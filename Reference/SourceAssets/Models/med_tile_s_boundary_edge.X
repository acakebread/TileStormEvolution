xof 0303txt 0032
template ColorRGBA {
 <35ff44e0-6c7c-11cf-8f52-0040333594a3>
 FLOAT red;
 FLOAT green;
 FLOAT blue;
 FLOAT alpha;
}

template ColorRGB {
 <d3e16e81-7835-11cf-8f52-0040333594a3>
 FLOAT red;
 FLOAT green;
 FLOAT blue;
}

template Material {
 <3d82ab4d-62da-11cf-ab39-0020af71e433>
 ColorRGBA faceColor;
 FLOAT power;
 ColorRGB specularColor;
 ColorRGB emissiveColor;
 [...]
}

template TextureFilename {
 <a42790e1-7810-11cf-8f52-0040333594a3>
 STRING filename;
}

template Frame {
 <3d82ab46-62da-11cf-ab39-0020af71e433>
 [...]
}

template Matrix4x4 {
 <f6f23f45-7686-11cf-8f52-0040333594a3>
 array FLOAT matrix[16];
}

template FrameTransformMatrix {
 <f6f23f41-7686-11cf-8f52-0040333594a3>
 Matrix4x4 frameMatrix;
}

template Vector {
 <3d82ab5e-62da-11cf-ab39-0020af71e433>
 FLOAT x;
 FLOAT y;
 FLOAT z;
}

template MeshFace {
 <3d82ab5f-62da-11cf-ab39-0020af71e433>
 DWORD nFaceVertexIndices;
 array DWORD faceVertexIndices[nFaceVertexIndices];
}

template Mesh {
 <3d82ab44-62da-11cf-ab39-0020af71e433>
 DWORD nVertices;
 array Vector vertices[nVertices];
 DWORD nFaces;
 array MeshFace faces[nFaces];
 [...]
}

template MeshNormals {
 <f6f23f43-7686-11cf-8f52-0040333594a3>
 DWORD nNormals;
 array Vector normals[nNormals];
 DWORD nFaceNormals;
 array MeshFace faceNormals[nFaceNormals];
}

template MeshMaterialList {
 <f6f23f42-7686-11cf-8f52-0040333594a3>
 DWORD nMaterials;
 DWORD nFaceIndexes;
 array DWORD faceIndexes[nFaceIndexes];
 [Material <3d82ab4d-62da-11cf-ab39-0020af71e433>]
}

template Coords2d {
 <f6f23f44-7686-11cf-8f52-0040333594a3>
 FLOAT u;
 FLOAT v;
}

template MeshTextureCoords {
 <f6f23f40-7686-11cf-8f52-0040333594a3>
 DWORD nTextureCoords;
 array Coords2d textureCoords[nTextureCoords];
}


Material PDX08_-_Default {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Med_texturepage.bmp";
 }
}

Frame tile_boundary_edge {
 

 FrameTransformMatrix {
  -0.000001,0.000000,1.000000,0.000000,-1.000000,0.000000,-0.000001,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000003,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   20;
   -0.500000;0.000000;-0.245000;,
   0.000003;0.000000;-0.245000;,
   -0.500000;0.000000;0.100000;,
   -0.000004;0.000000;0.100000;,
   -0.500000;1.000000;-0.245000;,
   -0.500000;1.000000;0.100000;,
   -0.000016;1.000000;-0.245000;,
   -0.000016;1.000000;0.100000;,
   -0.500000;0.000000;-0.245000;,
   -0.500000;0.000000;0.100000;,
   -0.500000;0.000000;-0.245000;,
   -0.500000;1.000000;-0.245000;,
   0.000003;0.000000;-0.245000;,
   -0.500000;1.000000;0.100000;,
   -0.000016;1.000000;-0.245000;,
   -0.500000;1.000000;-0.245000;,
   -0.000016;1.000000;-0.245000;,
   -0.000004;0.000000;0.100000;,
   0.000003;0.000000;-0.245000;,
   -0.000016;1.000000;0.100000;;
   10;
   3;4,2,5;,
   3;2,4,0;,
   3;3,8,1;,
   3;8,3,9;,
   3;6,10,11;,
   3;6,12,10;,
   3;13,14,15;,
   3;14,13,7;,
   3;16,17,18;,
   3;16,19,17;;

   MeshNormals  {
    9;
    -1.000000;0.000000;0.000000;,
    0.000000;-1.000000;0.000000;,
    0.000001;-1.000000;-0.000000;,
    0.000000;0.000000;-1.000000;,
    -0.000000;0.000000;-1.000000;,
    0.000000;1.000000;-0.000000;,
    -0.000000;1.000000;-0.000000;,
    1.000000;0.000019;0.000022;,
    1.000000;0.000012;0.000000;;
    10;
    3;0,0,0;,
    3;0,0,0;,
    3;1,1,1;,
    3;2,2,2;,
    3;3,3,3;,
    3;4,4,4;,
    3;5,5,5;,
    3;6,6,6;,
    3;7,7,7;,
    3;8,8,8;;
   }

   MeshMaterialList  {
    1;
    10;
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0;
    { PDX08_-_Default }
   }

   MeshTextureCoords  {
    20;
    0.621615;0.292733;,
    0.621374;0.293141;,
    0.621615;0.350987;,
    0.621345;0.352675;,
    0.448705;0.292733;,
    0.448705;0.350987;,
    0.761520;0.013199;,
    0.590072;0.352675;,
    0.449337;0.293141;,
    0.449365;0.352675;,
    0.701615;0.133014;,
    0.701615;0.013199;,
    0.761789;0.132908;,
    0.672878;0.352675;,
    0.590067;0.293141;,
    0.672906;0.293141;,
    0.622912;0.352934;,
    0.451387;0.293188;,
    0.451387;0.352934;,
    0.622912;0.293189;;
   }
  }
 }
}