--[==[

	大规模骨骼动画方案  TODO
	{
		1 人物模型换装
		{
			优化方案： 合并材质和贴图，减少DC，腾讯的NBA项目也用过

			缺点： 1 拆件的原始贴图需要开启Read/Write 2 texture.Packtexture接口耗时较高 3 生成的动态图格式无法修改（待验证）==》感觉跟原始图一样啊

			{
				
			//PackTextures接口需要原始图需要开启可读可写,
			//PackTextures接口调用后，会修改newDiffuseTex的format，修改的结果跟需要打包的图的格式有关（保持一致）
			//PackTextures接口运行时它不支持任意贴图格式的合并，比如现在主流的ASTC，它会合并成RGBA32格式
			}
		}

		2 从CPU层蒙皮计算移到GPU层
		{
			方案1：动作的每一帧顶点数据都都绘制到贴图里
			{
				缺点： 贴图内存占用较大，动画过度效果不好
			}

			方案2：将骨骼的矩阵烘焙到纹理上，将权重和索引写入Mesh中，在顶点着色器中读出矩阵做变换
		}

		https://github.com/chengkehan/GPUSkinning

	}






]==]