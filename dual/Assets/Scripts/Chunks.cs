using UnityEngine;
using System.Collections;
using System.IO;

public unsafe class Chunk{
	public Vector3 pos;
	Vector3 absPos;
	public int state;
	int id;
	GameObject host;
	public int memId;
	public uint* chunkMem;
	public int MemLength;
	int[] borders;
	int LodLevel;
	public Chunk(Vector3 Pos,int Id){
		MemLength = 0;
		memId = -1;
		pos = Pos;

		id = Id;
		absPos = Pos*6.4f;
		state = (int)ChunkState.UnInitialized;
		host = new GameObject();//GameObject.Instantiate(new GameObject(), absPos, Quaternion.identity) as GameObject;
		host.transform.position = absPos;
		host.name = "Chunk_"+id;
		borders = new int[1+(int)Mathf.Log((float)Terrain_Handler.ChunkSize.x,2.0f)];
		LodLevel = borders.Length;
		render(LodLevel);
		Terrain_Handler.renderman.AddToRenderQueue(pos,memId);
	}

	~Chunk(){
		if(chunkMem != null){
			//GameObject.Destroy(host,0.0f);
			Unmanaged.Free(chunkMem);
		}
	}

	public void render(int lvl){
		if(memId == -1){
			if(Terrain_Handler.chunkInitKernel1 == -1){
				Terrain_Handler.chunkInitKernel1 = CL_Handler.createKernel("chunkInit1");
				Terrain_Handler.chunkInitKernel2 = CL_Handler.createKernel("chunkInit2");
				Terrain_Handler.chunkScanKernel = CL_Handler.createKernel("BScan");
				Terrain_Handler.chunkMemCpyKernel = CL_Handler.createKernel("chunkMemCpy");
			}
			float t = Time.realtimeSinceStartup;
			InitChunk();
			Debug.Log("Succesful Chunk initialization in "+(Time.realtimeSinceStartup-t)*1000+"ms");
			saveToFile();

		}
	}

	public void checkFirst(Vector3 relPos){
		Vector3 bPos = new Vector3(0,0,0);
		uint block = chunkMem[MemLength-1];
    //float4 bPos = (float4)(0.0,0.0,0.0,0.0);
    uint off = 0;
    uint lvl = 64;//TODO:Hardcoded Max size 128?
    while((block&0xFF000000) != 0xC0000000){
        off |= relPos.x>=(bPos.x+lvl)? (uint)1:0;
        off |= relPos.y>=(bPos.y+lvl)? (uint)2:0;
        off |= relPos.z>=(bPos.z+lvl)? (uint)4:0;
        block = chunkMem[((block&0x00FFFFFF))+off];
        bPos += new Vector3((off&1)*lvl,((off&2)>>1)*lvl,((off&4)>>2)*lvl);
				lvl = lvl>>1;
				off = 0;
				//Debug.Log(bPos+"\n");
    }
    block &= 0x00FFFFFF;
    //block |= lvl<<25;
		Debug.Log(block+" Done");
		//return block&0x00FFFFFF;
	}

	private int average(uint a,uint b,uint c,uint d,uint e,uint f,uint g,uint h){
	  return (int)Mathf.Floor(((0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a))*0.125f);
	}

	public void InitChunk(){
		//ChunkInit
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkInitKernel1,0,CL_Handler.tmpChunkDataId);
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkInitKernel1,1,CL_Handler.RandomNumberMemId);
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkInitKernel1,2,Terrain_Handler.NoiseCount,sizeof(int));
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkInitKernel1,3,Terrain_Handler.NoiseSize,sizeof(int));
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkInitKernel1,4,(int)(pos.x*Terrain_Handler.ChunkSize.x),sizeof(int));
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkInitKernel1,5,(int)(pos.y*Terrain_Handler.ChunkSize.y),sizeof(int));
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkInitKernel1,6,(int)(pos.z*Terrain_Handler.ChunkSize.z),sizeof(int));
		int* globalWorkSize = (int*) Unmanaged.New<int>(3);
		int* localWorkSize = (int*) Unmanaged.New<int>(3);
		globalWorkSize[0] = (int)Terrain_Handler.ChunkSize.x/2;
		globalWorkSize[1] = (int)Terrain_Handler.ChunkSize.y/2;
		globalWorkSize[2] = (int)Terrain_Handler.ChunkSize.z/2;
		localWorkSize [0] = (int)8;
		localWorkSize [1] = (int)8;
		localWorkSize [2] = (int)8;
		CL_Handler.DispatchKernel(Terrain_Handler.chunkInitKernel1,3,globalWorkSize,localWorkSize,false);

		//CL_Handler.MemCpy_ClientToHost(CL_Handler.tmpChunkDataId);
		//DebugSave();
		//return;

		int AbsSize = (int)(Terrain_Handler.ChunkSize.x*Terrain_Handler.ChunkSize.y*Terrain_Handler.ChunkSize.z);
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkInitKernel2,0,CL_Handler.tmpChunkDataId);
		int dispachSize = AbsSize;
		int i = 0;
		//Debug.Log("Abssize : "+AbsSize);
		for(i = (int)(AbsSize)>>16; i>0; i>>=1){
			//Debug.Log("i ="+i+"; dispachSize ="+dispachSize+";");
			CL_Handler.SetKernelArgValue(Terrain_Handler.chunkInitKernel2,1,dispachSize,sizeof(int));
			//Debug.Log("Dispatch size: "+dispachSize);
			globalWorkSize[0] = (int)(i);
			globalWorkSize[1] = (int)(i);
			globalWorkSize[2] = (int)(i);
			CL_Handler.DispatchKernel(Terrain_Handler.chunkInitKernel2,3,globalWorkSize,null,false);
			dispachSize += i*i*i*8;
		}

		//CL_Handler.MemCpy_ClientToHost(CL_Handler.tmpChunkDataId);
		//int sdf = 0;
		//DebugSave();
		//CPU finsih

		/*for(i=i; i>0; i >>= 1){
			for(int j = 0; j < (i<<6); j++){
		    uint a,b,c,d,e,f,g,h;

				int x = j % i;
				int y = (j / i) % i;
				int z = j / (i * i);

		    int id = j*8;

		    a = CL_Handler.tmpChunkData[dispachSize+id  ];
		    b = CL_Handler.tmpChunkData[dispachSize+id+1];
		    c = CL_Handler.tmpChunkData[dispachSize+id+2];
		    d = CL_Handler.tmpChunkData[dispachSize+id+3];
		    e = CL_Handler.tmpChunkData[dispachSize+id+4];
		    f = CL_Handler.tmpChunkData[dispachSize+id+5];
		    g = CL_Handler.tmpChunkData[dispachSize+id+6];
		    h = CL_Handler.tmpChunkData[dispachSize+id+7];

		    if(a == b &&a == c &&a == d &&a == e &&a == f &&a == g &&a == h&&
		    (a&0x40000000)!=0){
		        CL_Handler.tmpChunkData[dispachSize+id  ] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+1] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+2] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+3] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+4] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+5] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+6] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+id+7] = (int) 0x00000000;
		        CL_Handler.tmpChunkData[dispachSize+(i<<3)+
		                (int)(x/2.0f)*8+x%2+
		                ((int)(y/2.0f)*8)*(i>>1)+y%2+
		                ((int)(z/2.0f)*8)*((i*i)>>2)+z%2] = (0xC0000000)|(0x00FFFFFF&a);
		    }else{

		        CL_Handler.tmpChunkData[dispachSize+(i<<3)+
		                (int)(x/2.0f)*8+x%2+
		                ((int)(y/2.0f)*8)*(i>>1)+y%2+
		                ((int)(z/2.0f)*8)*((i*i)>>2)+z%2] = (uint) ((0x80000000)|(0x00FFFFFF&(dispachSize+id)));
		    }
			}
			dispachSize += i*i*i*8;
		}*/

		//DebugSave();

		//CL_Handler.MemCpy_HostToClient(CL_Handler.tmpChunkDataId);

		//DebugSave();
		//Dispatch chunkscan

		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkScanKernel,0,CL_Handler.tmpChunkDataId);
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkScanKernel,1,CL_Handler.tmpScanDataId);
		CL_Handler.SetKernelArgLocalMem(Terrain_Handler.chunkScanKernel,2,(uint)(Terrain_Handler.MaxChunkSize)*sizeof(int));//TODO:Hardcoded Max Work Size
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkScanKernel,3,Terrain_Handler.MaxChunkSize,sizeof(int));
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkScanKernel,4,CL_Handler.tmpDataSize,sizeof(int));
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkScanKernel,5,CL_Handler.tmpScanLengthDataId);
		globalWorkSize[0] = (int)CL_Handler.tmpDataSizeCube>>1;
		globalWorkSize[1] = (int)1;
		globalWorkSize[2] = (int)1;
		localWorkSize [0] = (int)Terrain_Handler.MaxChunkSize>>1;
		localWorkSize [1] = (int)1;
		localWorkSize [2] = (int)1;

		CL_Handler.DispatchKernel(Terrain_Handler.chunkScanKernel,1,globalWorkSize,localWorkSize,false);

		CL_Handler.MemCpy_ClientToHost(CL_Handler.tmpScanLengthDataId);


		int dChunkSize = CL_Handler.tmpDataSizeCube/Terrain_Handler.MaxChunkSize;
		uint tmpFirst = 0;
		uint tmpSecond = 0;
		for(int iter = 0; iter < dChunkSize; iter++){
			MemLength += (int)CL_Handler.tmpScanLengthData[iter];
			if(iter>0){
				tmpSecond = CL_Handler.tmpScanLengthData[iter];
				CL_Handler.tmpScanLengthData[iter] = tmpFirst;
				tmpFirst = tmpSecond+tmpFirst;
			}else{
				tmpFirst = CL_Handler.tmpScanLengthData[0];
				CL_Handler.tmpScanLengthData[0] = 0;
			}
		}

		Debug.Log("MemLength :"+MemLength);

		CL_Handler.MemCpy_ClientToHost(CL_Handler.tmpScanDataId);//TODO:Slow?
		//DebugSave();

		//create memory
		chunkMem = (uint*) Unmanaged.New<uint>((int)MemLength);
		memId = CL_Handler.AllocateHostMem(chunkMem,MemLength,sizeof(int));
		CL_Handler.AllocateClientMem(0,memId);

		CL_Handler.MemCpy_HostToClient(CL_Handler.tmpScanLengthDataId);
		CL_Handler.MemCpy_HostToClient(CL_Handler.tmpScanDataId);


		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkMemCpyKernel,0,CL_Handler.tmpChunkDataId);
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkMemCpyKernel,1,CL_Handler.tmpScanDataId);
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkMemCpyKernel,2,memId);
		CL_Handler.SetKernelArgMem(Terrain_Handler.chunkMemCpyKernel,3,CL_Handler.tmpScanLengthDataId);
		CL_Handler.SetKernelArgValue(Terrain_Handler.chunkMemCpyKernel,4,Terrain_Handler.MaxChunkSize,sizeof(int));
		globalWorkSize[0] = (int)CL_Handler.tmpDataSizeCube;
		//Debug.Log(CL_Handler.tmpDataSizeCube);
		CL_Handler.DispatchKernel(Terrain_Handler.chunkMemCpyKernel,1,globalWorkSize,null,false);

		//Copy Mem to chunkMem
		CL_Handler.MemCpy_ClientToHost(memId);
		Unmanaged.Free(globalWorkSize);
		Unmanaged.Free(localWorkSize);
		state = (int)ChunkState.Rendered;
	}

	void UnPackData(int lvl){
		int cumulaitiveCount = 1;
		int id;
		for(int i = 1; i < Mathf.Pow(8.0f,(float)lvl) && i<512; i<<=1){
		//	if(chunkMem){

		//	}
		}
	}

	public void saveToFile(){
		BinaryWriter writer = new BinaryWriter(File.Open((string)(Application.dataPath+"Chunk"+id+".dat"), FileMode.Create));
		for(int i = 0; i < MemLength;i++){
			uint ret = (chunkMem[i]&0xFF000000)>>24;
			ret = ret | ((chunkMem[i]&0x00FF0000)>>8);
			ret = ret | ((chunkMem[i]&0x0000FF00)<<8);
			ret = ret | ((chunkMem[i]&0x000000FF)<<24);
			writer.Write(ret);
		//	writer.Write(chunkMem[i]);
		}
	}

	public void DebugSave(){
		BinaryWriter writer = new BinaryWriter(File.Open((string)(Application.dataPath+"ChunkDebug"+id+".dat"), FileMode.Create));
		//Debug.Log("now tmp data size:"+CL_Handler.tmpDataSize);
		//Debug.Log("Whoop "+CL_Handler.tmpChunkData[CL_Handler.tmpDataSize-1]);
		for(int i = 0; i < CL_Handler.tmpDataSize;i++){
			uint ret = (CL_Handler.tmpChunkData[i]&0xFF000000)>>24;
			ret = ret | ((CL_Handler.tmpChunkData[i]&0x00FF0000)>>8);
			ret = ret | ((CL_Handler.tmpChunkData[i]&0x0000FF00)<<8);
			ret = ret | ((CL_Handler.tmpChunkData[i]&0x000000FF)<<24);
			writer.Write(ret);
		}
	}

	public void drawDebugBox (Color col){
		Debug.DrawLine(pos, pos+Vector3.up*128, col);
		Debug.DrawLine(pos, pos+Vector3.forward*128, col);
		Debug.DrawLine(pos, pos+Vector3.right*128, col);

		Debug.DrawLine(pos+Vector3.right*128+Vector3.up*128, pos+Vector3.right*128, col);
		Debug.DrawLine(pos+Vector3.right*128+Vector3.up*128, pos+Vector3.up*128, col);
		Debug.DrawLine(pos+Vector3.right*128+Vector3.up*128, pos+Vector3.forward*128+Vector3.right*128+Vector3.up*128, col);

		Debug.DrawLine(pos+Vector3.forward*128+Vector3.up*128, pos+Vector3.forward*128, col);
		Debug.DrawLine(pos+Vector3.forward*128+Vector3.up*128, pos+Vector3.up*128, col);
		Debug.DrawLine(pos+Vector3.forward*128+Vector3.up*128, pos+Vector3.forward*128+Vector3.right*128+Vector3.up*128, col);

		Debug.DrawLine(pos+Vector3.forward*128+Vector3.right*128, pos+Vector3.forward*128, col);
		Debug.DrawLine(pos+Vector3.forward*128+Vector3.right*128, pos+Vector3.right*128, col);
		Debug.DrawLine(pos+Vector3.forward*128+Vector3.right*128, pos+Vector3.forward*128+Vector3.right*128+Vector3.up*128, col);
	}
}

public enum ChunkState : int {
	UnInitialized = 0, Idle = 1, Rendered = 2
}
