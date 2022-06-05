//#define USE_RAW

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class UCombineSkinnedMgr
{

	/// <summary>
	/// Only for merge materials.
	/// </summary>
	private const int COMBINE_TEXTURE_MAX = 512;
	private const string COMBINE_DIFFUSE_TEXTURE = "_MainTex";

	/// <summary>
	/// Combine SkinnedMeshRenderers together and share one skeleton.
	/// Merge materials will reduce the drawcalls, but it will increase the size of memory. 
	/// </summary>
	/// <param name="skeleton">combine meshes to this skeleton(a gameobject) 骨骼对象</param>
	/// <param name="meshes">meshes need to be merged  需要合并的mesh，部件的mesh</param>
	/// <param name="combine">merge materials or not</param>
	public void CombineObject(GameObject skeleton, SkinnedMeshRenderer[] meshes, bool combine = false)
	{

		// Fetch all bones of the skeleton
		List<Transform> transforms = new List<Transform>();
		transforms.AddRange(skeleton.GetComponentsInChildren<Transform>(true)); //骨骼对象的transform

		List<Material> materials = new List<Material>();//the list of materials
		List<CombineInstance> combineInstances = new List<CombineInstance>();//the list of meshes
		List<Transform> bones = new List<Transform>();//the list of bones

		// Below informations only are used for merge materilas(bool combine = true)
		List<Vector2[]> oldUV = null;
		Material newMaterial = null;
		Texture2D newDiffuseTex = null;
#if USE_RAW
        int uvCount = 0;
        List<Vector2[]> uvList = new List<Vector2[]>();
		Vector2[] atlasUVs = null;
#endif

		// Collect information from meshes 
		for (int i = 0; i < meshes.Length; i++)
		{
			SkinnedMeshRenderer smr = meshes[i];
			materials.AddRange(smr.sharedMaterials); // Collect materials
													 // Collect meshes 合并网格
			for (int sub = 0; sub < smr.sharedMesh.subMeshCount; sub++)
			{
				CombineInstance ci = new CombineInstance();
				ci.mesh = smr.sharedMesh; //部件的mesh
				ci.subMeshIndex = sub;
				combineInstances.Add(ci);
			}
			// Collect bones 合并骨骼
			for (int j = 0; j < smr.bones.Length; j++)
			{
				int tBase = 0;
				for (tBase = 0; tBase < transforms.Count; tBase++)
				{
					//smr.bones ==> SkinMeshRender上记录的所有骨骼
					//transforms ==> 存储的是骨骼prefab对应的transform信息，骨骼在Unity里就是以Transform形式存在
					if (smr.bones[j].name.Equals(transforms[tBase].name))
					{
						bones.Add(transforms[tBase]); //把骨骼信息存起来，从骨骼prefab对应的transform信息
						break;
					}
				}
			}

#if USE_RAW
            uvList.Add(smr.sharedMesh.uv);
            uvCount += smr.sharedMesh.uv.Length;
#endif

        }

		// merge materials
		if (combine)
		{
			newMaterial = new Material(Shader.Find("Mobile/Diffuse"));
			oldUV = new List<Vector2[]>();
			// merge the texture
			List<Texture2D> Textures = new List<Texture2D>();
			int texW = 0;
			int texH = 0;
			for (int i = 0; i < materials.Count; i++)
			{
				Texture2D tex = materials[i].GetTexture(COMBINE_DIFFUSE_TEXTURE) as Texture2D;
				Textures.Add(tex);
				texW += tex.width;
				texH += tex.height;
			}

#if USE_RAW
			//默认规定4张图
			//4张图最好格式一样，
			Rect[] rec = new Rect[4];
			rec[0].xMin = 0; rec[0].xMax = 0.25f; rec[0].yMin = 0; rec[0].yMax = 0.25f;
			rec[1].xMin = 0.25f; rec[1].xMax = 0.5f; rec[1].yMin = 0f; rec[1].yMax = 0.25f;
			rec[2].xMin = 0; rec[2].xMax = 0.25f; rec[2].yMin = 0.25f; rec[2].yMax = 0.5f;
			rec[3].xMin = 0.25f; rec[3].xMax = 0.5f; rec[3].yMin = 0.25f; rec[3].yMax = 0.375f;

			// 记录下每张图的大小
			int xyMax = 1024;
			
			int blockByte = getBlcokBytes(Textures[0], xyMax);
   //         blockByte = getBlcokBytes(Textures[1], 1024);
			//blockByte = getBlcokBytes(Textures[2], 1024);
			//blockByte = getBlcokBytes(Textures[3], 1024);
			getByteInTx(rec[0].xMin, rec[0].yMin, data, blockByte, xyMax, Textures[0]);
			getByteInTx(rec[1].xMin, rec[1].yMin, data, blockByte, xyMax, Textures[1]);
            getByteInTx(rec[2].xMin, rec[2].yMin, data, blockByte, xyMax, Textures[2]);
            getByteInTx(rec[3].xMin, rec[3].yMin, data, blockByte, xyMax, Textures[3]);

  
            var combinedTex = new Texture2D(xyMax, xyMax, Textures[0].format, false);
            combinedTex.LoadRawTextureData(data);
            combinedTex.Apply(false, true);

			newMaterial.mainTexture = combinedTex;

			atlasUVs = new Vector2[uvCount];
			//as combine textures into single texture,so need recalculate uvs			 
			int j = 0;
            for (int i = 0; i < uvList.Count; i++)
            {
                foreach (Vector2 uv in uvList[i])
                {
					atlasUVs[j].x = Mathf.Lerp(rec[i].xMin, rec[i].xMax, uv.x);
					atlasUVs[j].y = Mathf.Lerp(rec[i].yMin, rec[i].yMax, uv.y);
                    int sq = i + 1;
                    j++;
                }
			}


#else
			//统计合并后texture大小
			texW = Get2Pow(texW);
			texH = Get2Pow(texH);

			newDiffuseTex = new Texture2D(texW, texH, TextureFormat.RGBA32, true);
			newDiffuseTex.name = "dddddddddd";
			Debug.Log($"targetTexture Size==============={texW} {texH}");
			Debug.Log($"before PackTexures==============={newDiffuseTex.format}");

			//PackTextures接口需要原始图需要开启可读可写,
			//PackTextures接口调用后，会修改newDiffuseTex的format，修改的结果跟需要打包的图的格式有关（保持一致）
			//PackTextures接口运行时它不支持任意贴图格式的合并，比如现在主流的ASTC，它会合并成RGBA32格式

			Rect[] uvs = newDiffuseTex.PackTextures(Textures.ToArray(), 0);
			Debug.Log($"after PackTexures==============={newDiffuseTex.format}");
			newMaterial.mainTexture = newDiffuseTex;

			// reset uv 因为合并了贴图,需要重新计算新UV值
			Vector2[] uva, uvb;
			for (int j = 0; j < combineInstances.Count; j++)
			{
				uva = (Vector2[])(combineInstances[j].mesh.uv);  //原始部件Mesh的UV信息
				uvb = new Vector2[uva.Length];
				for (int k = 0; k < uva.Length; k++)
				{
					//计算合并贴图后新的UV信息
					uvb[k] = new Vector2((uva[k].x * uvs[j].width) + uvs[j].x, (uva[k].y * uvs[j].height) + uvs[j].y);
				}
				oldUV.Add(combineInstances[j].mesh.uv);
				combineInstances[j].mesh.uv = uvb; //更新UV
			}
#endif



        }

		// Create a new SkinnedMeshRenderer  在根节点上
		SkinnedMeshRenderer oldSKinned = skeleton.GetComponent<SkinnedMeshRenderer>();
		if (oldSKinned != null)
		{

			GameObject.DestroyImmediate(oldSKinned);
		}
		SkinnedMeshRenderer r = skeleton.AddComponent<SkinnedMeshRenderer>();
		r.sharedMesh = new Mesh();
		//最后调用Mesh的合并接口
		r.sharedMesh.CombineMeshes(combineInstances.ToArray(), combine, false);// Combine meshes
		r.bones = bones.ToArray();// Use new bones
		if (combine)
		{
			r.material = newMaterial;
#if USE_RAW
			r.sharedMesh.uv = atlasUVs;
#else
		//为什么要重置UV？？？ ==》 因为要恢复原始部件上的Mesh的UV信息，下次组装的时候才能拿到未修改的数据进行合并
			for (int i = 0; i < combineInstances.Count; i++)
			{
				combineInstances[i].mesh.uv = oldUV[i];
			}
#endif


        }
        else
		{
			r.materials = materials.ToArray();
		}
	}


	public int Get2Pow(int into)
	{
		int outo = 1;
		for (int i = 0; i < 10; i++)
		{
			outo *= 2;
			if (outo > into)
			{
				break;
			}
		}

		return outo;
	}

	public byte[] data;
	public void getByteInTx(float Vx, float Vy, byte[] dst, int bytes, int length, Texture2D tex)
	{
		var len = tex.width;
		var Proportion = length / len;
		Vx = Vx * Proportion;
		Vy = Vy * Proportion;
		if (Vx >= Proportion || Vy >= Proportion)
		{
			return;
		}
		CombineBlocks(tex.GetRawTextureData(), dst, (int)Vx * tex.width, (int)Vy * tex.width, tex.width, 4, bytes, length);
	}

	void CombineBlocks(byte[] src, byte[] dst, int dstx, int dsty, int width, int block, int bytes, int length)
	{
		var dstbx = dstx / block;
		var dstby = dsty / block;

		for (int i = 0; i < width / block; i++)
		{
			int dstindex = (dstbx + (dstby + i) * (length / block)) * bytes;
			int srcindex = i * (width / block) * bytes;
			Buffer.BlockCopy(src, srcindex, dst, dstindex, width / block * bytes);
		}
	}

    public int getBlcokBytes(Texture2D tex, int length)
    {
        int blcokBytes = 0;
        data = null;
        switch (tex.format)
        {
            case TextureFormat.DXT1:
            case TextureFormat.ETC_RGB4:
            case TextureFormat.ETC2_RGB:
                blcokBytes = 8;
                data = new byte[length / 2 * length];
                break;
            case TextureFormat.DXT5:
            case TextureFormat.ETC2_RGBA8:
            case TextureFormat.ASTC_RGB_4x4:
            case TextureFormat.ASTC_RGBA_4x4:
                blcokBytes = 16;
                data = new byte[length * length];
                break;
            default:
               Debug.LogError("不支持的合图格式：" + tex.format);
                return 0;
        }
        //128 && 256 && 512  合起来填充的1024 的  记住传参要合理
        //CombineBlock(0, 0,  data, blcokBytes, length, tex);
        //CombineBlock(0.25f, 0,  data, blcokBytes, length, tex);
        //CombineBlock(0, 0.25f,  data, blcokBytes, length, tex);
        //CombineBlock(0.25f, 0.25f, data, blcokBytes, length, tex1);
        //CombineBlock(0.25f, 0.375f, data, blcokBytes, length, tex1);
        //CombineBlock(0.375f, 0.25f, data, blcokBytes, length, tex1);
        //CombineBlock(0.375f, 0.375f, data, blcokBytes, length, tex1);
        //CombineBlock(0.5f, 0, data, blcokBytes, length, texture512);
        //CombineBlock(0.5f, 0.5f, data, blcokBytes, length, texture512);
        //CombineBlock(0, 0.5f, data, blcokBytes, length, texture512);
        return blcokBytes;
    }
}
