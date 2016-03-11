import java.io.*;

//TODO output all in color

public class debugOctree {
  public static final String ANSI_RESET = "\u001B[0m";
  public static final String ANSI_BLACK = "\u001B[30m";
  public static final String ANSI_RED = "\u001B[31m";
  public static final String ANSI_GREEN = "\u001B[32m";
  public static final String ANSI_YELLOW = "\u001B[33m";
  public static final String ANSI_BLUE = "\u001B[34m";
  public static final String ANSI_PURPLE = "\u001B[35m";
  public static final String ANSI_CYAN = "\u001B[36m";
  public static final String ANSI_WHITE = "\u001B[37m";
  public static int[] data;
  public static void main (String[] args){
    //System.out.println(ANSI_RED + "This text is red!" + ANSI_RESET);
    int cObj = 0;
    int[] cData;
    System.out.println("Insert file path:");
    String spath = System.console().readLine().trim();
    if(spath.isEmpty()){
      spath = "/Users/sebastian/UnityProjects/dual/AssetsChunk0.dat";
    }
    cObj = loadFile(spath);
    if(cObj == 0){
      return;
    }
    cData = new int[data.length];

    //logAll(data,cData);

    Tree myTree = new Tree(cObj,data);
    myTree.Log(0);
    while(true){
      System.out.println("Insert OP:");
      String inStr = System.console().readLine();
      //System.out.println("Test :"+(inStr.split("\\|"))[0]+":End");
      if((inStr.split(" "))[0].equals("open")){
        myTree.openPath((inStr.split(" "))[1],data);
        //myTree.Log(2);
      }
      if((inStr.split(" "))[0].equals("log")){
        myTree.Log(Integer.parseInt( inStr.split(" ")[1]));
      }
      if((inStr.split(" "))[0].equals("block")){
        EvaluateBlock(
          Integer.parseInt(((inStr.split(" "))[1]).split(",")[0]),
          Integer.parseInt(((inStr.split(" "))[1]).split(",")[1]),
          Integer.parseInt(((inStr.split(" "))[1]).split(",")[2]),
          data,
          data.length-1
          ) ;
      }
      if((inStr.split(" "))[0].equals("quit")){
        return;
      }
    }
  }

  public static void EvaluateBlock(int relX,int relY,int relZ,int[] chunk,int lastI){
    float bPosX = 0, bPosY = 0, bPosZ = 0;
    int block = chunk[lastI];
    //float4 bPos = (float4)(0.0,0.0,0.0,0.0);
    int off = 0;
    int lvl = 64;//TODO:Hardcoded Max size 128?

    while((block&0xFF000000) != 0xC0000000){
        off |= (int)(relX>(bPosX+lvl)? 1:0);
        off |= (int)(relY>(bPosY+lvl)? 2:0);
        off |= (int)(relZ>(bPosZ+lvl)? 4:0);
        block = chunk[((block&0x00FFFFFF))+off];
        //bPos += (float4)((off&1)*lvl,((off&2)>>1)*lvl,((off&4)>>2)*lvl,0.0);
        bPosX += (off&1)*lvl;
        bPosY += ((off&2)>>1)*lvl;
        bPosZ += ((off&4)>>2)*lvl;
        lvl = lvl>>1;
        off = 0;
    }
    //return block&0x00FFFFFF;
    System.out.println("block :"+(block&0x00FFFFFF));
    //return chunk[(int)(relPos.x*128+relPos.y)]&0x00FFFFFF;
}

  public static int loadFile(String path){
    long fLength = (new File(path)).length();
    data = new int[(int)(fLength*0.25)];
    try{
      DataInputStream instr = new DataInputStream(new BufferedInputStream(new FileInputStream(path)));
      for(int i = 0; i < data.length;i++){
        data[i] = instr.readInt();
      }
      //FileInputStream fs = new FileInputStream(path);
    }catch (FileNotFoundException ex)
    {
      System.out.println("File not Found.");
      System.out.println(path);
      return 0;
    }catch ( IOException iox )
    {
      System.out.println("unreport IO.");
      return 0;
    }
    System.out.println("File length: "+fLength);

    return data[data.length-1];
  }

  public static void logAll(int[] d,int[] cd){
    if((d[d.length-1]&0xFF000000)!= 0xC0000000){
      dig(d,cd, (d[d.length-1]&0x00FFFFFF) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+1) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+2) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+3) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+4) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+5) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+6) ,0);
      dig(d,cd, ((d[d.length-1]&0x00FFFFFF)+7) ,0);
    }
    for(int step = 0; step < 1024; step++){
      int substep = d.length/1024;
      int cStep = step*substep;
      String s = "";
      for(int i = cStep; i < cStep+substep; i++){
        switch(cd[d.length-1-i]){
          case 0:s+=ANSI_RED;break;
          case 1:s+=ANSI_GREEN;break;
          case 2:s+=ANSI_BLUE;break;
          case 3:s+=ANSI_CYAN;break;
          case 4:s+=ANSI_BLACK;break;
          case 5:s+=ANSI_YELLOW;break;
          default:s+=ANSI_PURPLE;break;
        }

        if((d[d.length-1]&0xFF000000)!= 0xC0000000){
          s+="#";
        }else{
          s+="O";
        }
        s += ANSI_RESET;
        //s+="|"+cd[d.length-1-i]+"|";
        //s += "|"+d[d.length-1-i]+"|";
      }
      System.out.print(s);
    }
    /*
    for(int i = 0; i < d.length;i++){
      if(cd[d.length-1-i] != 0){

      }else{
        if((d[d.length-1-i]&0xFF000000)!= 0xC0000000){

        }else{
          cd[d.length-1-i] = 1;
        }
      }
    }*/
  }

  public static void dig(int[] d,int[] cd,int i,int lvl){
    if((d[i]&0xFF000000)!= 0xC0000000){
      cd[d[i]&0x00FFFFFF] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+1] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+2] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+3] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+4] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+5] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+6] = lvl+1;
      cd[(d[i]&0x00FFFFFF)+7] = lvl+1;
      dig(d,cd,(d[i]&0x00FFFFFF),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+1),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+2),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+3),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+4),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+5),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+6),lvl+1);
      dig(d,cd,((d[i]&0x00FFFFFF)+7),lvl+1);
    }else{
      cd[i] = 1;
    }
  }

}

class Tree {
  Branch branches;
  public Tree(int s,int[] data){
    if((s&0xFF000000) != 0xC0000000){
      branches = new Branch(s,data);
    }else{
      branches = new Branch(s);
    }
  }

  public void Log(int lvl){
    for(int i = 0; i < lvl+1;i++){
      String s = branches.Log(i);
      System.out.println(s);
    }
  }

  public void openPath(String path,int[] data){
    branches.open(path.split(","),0,data);
  }
}

class Branch{
  Branch[] leafs;
  int d;
  boolean leafy;
  public Branch(int ds){
    d = ds;
    leafy = true;
  }
  public Branch(int s,int[] data){
    if(data.length != 1){
      leafs = new Branch[8];
      leafs[0] = new Branch(data[(s&0x00FFFFFF)  ]);
      leafs[1] = new Branch(data[(s&0x00FFFFFF)+1]);
      leafs[2] = new Branch(data[(s&0x00FFFFFF)+2]);
      leafs[3] = new Branch(data[(s&0x00FFFFFF)+3]);
      leafs[4] = new Branch(data[(s&0x00FFFFFF)+4]);
      leafs[5] = new Branch(data[(s&0x00FFFFFF)+5]);
      leafs[6] = new Branch(data[(s&0x00FFFFFF)+6]);
      leafs[7] = new Branch(data[(s&0x00FFFFFF)+7]);
      d = s;
      leafy = false;
    }else{
      leafy = true;
      d = s;
    }
  }
  public String Log(int lvl){
    if(lvl == 0){
      return (((d&0xFF000000)==0xC0000000)?"L":"N")+" "+(d&0x00FFFFFF)+" | ";
    }else{
      if(!leafy){
        return
        leafs[0].Log(lvl-1) +
        leafs[1].Log(lvl-1) +
        leafs[2].Log(lvl-1) +
        leafs[3].Log(lvl-1) +
        leafs[4].Log(lvl-1) +
        leafs[5].Log(lvl-1) +
        leafs[6].Log(lvl-1) +
        leafs[7].Log(lvl-1)
        ;
      }else{
        return "";
      }
    }
  }

  public void open (String[] path,int lvl,int[] data){
    if(!leafy){
      if(path.length <= lvl){

      }else{
        leafs[Integer.parseInt(path[lvl])].open(path,lvl+1,data);
      }
    }else{
      if((d&0xFF000000)!=0xC0000000){
        leafs = new Branch[8];

        leafs[0] = new Branch(data[d&0x00FFFFFF  ]);
        leafs[1] = new Branch(data[(d&0x00FFFFFF)+1]);
        leafs[2] = new Branch(data[(d&0x00FFFFFF)+2]);
        leafs[3] = new Branch(data[(d&0x00FFFFFF)+3]);
        leafs[4] = new Branch(data[(d&0x00FFFFFF)+4]);
        leafs[5] = new Branch(data[(d&0x00FFFFFF)+5]);
        leafs[6] = new Branch(data[(d&0x00FFFFFF)+6]);
        leafs[7] = new Branch(data[(d&0x00FFFFFF)+7]);

        leafy = false;
      }
    }
  }
}
