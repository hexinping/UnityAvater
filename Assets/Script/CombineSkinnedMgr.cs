using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UCombineSkinnedMgr {

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
	public void CombineObject (GameObject skeleton, SkinnedMeshRenderer[] meshes, bool combine = false){

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

		// Collect information from meshes 
		for (int i = 0; i < meshes.Length; i ++)
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
			for (int j = 0 ; j < smr.bones.Length; j ++)
			{
				int tBase = 0;
				for (tBase = 0; tBase < transforms.Count; tBase ++)
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
		}

        // merge materials
		if (combine)
		{
			newMaterial = new Material (Shader.Find ("Mobile/Diffuse"));
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
			//统计合并后texture大小
			texW = Get2Pow(texW);
			texH = Get2Pow(texH);
			newDiffuseTex = new Texture2D(texW, texH, TextureFormat.RGBA32, true);
			Rect[] uvs = newDiffuseTex.PackTextures(Textures.ToArray(), 0);
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
		}

		// Create a new SkinnedMeshRenderer  在根节点上
		SkinnedMeshRenderer oldSKinned = skeleton.GetComponent<SkinnedMeshRenderer> ();
		if (oldSKinned != null) {

			GameObject.DestroyImmediate (oldSKinned);
		}
		SkinnedMeshRenderer r = skeleton.AddComponent<SkinnedMeshRenderer>();
		r.sharedMesh = new Mesh();
		//最后调用Mesh的合并接口
		r.sharedMesh.CombineMeshes(combineInstances.ToArray(), combine, false);// Combine meshes
		r.bones = bones.ToArray();// Use new bones
		if (combine)
		{
			r.material = newMaterial;
            //为什么要重置UV？？？ ==》 因为要恢复原始部件上的Mesh的UV信息，下次组装的时候才能拿到未修改的数据进行合并
            for (int i = 0; i < combineInstances.Count; i++)
            {
                combineInstances[i].mesh.uv = oldUV[i];
            }
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
		
}
