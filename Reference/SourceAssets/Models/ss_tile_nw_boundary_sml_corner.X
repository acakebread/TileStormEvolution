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


Material PDX01_-_Default {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Ind_texturepage_new_01.tga";
 }
}

Material pipes {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Ind_pipes_1a.tga";
 }
}

Frame boundary_small_corner {
 

 FrameTransformMatrix {
  -0.000001,0.000000,1.000000,0.000000,-1.000000,-0.000000,-0.000001,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000003,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   15;
   0.500008;1.000000;-0.200002;,
   0.500000;1.000000;0.100000;,
   0.500000;0.500007;0.100000;,
   0.500008;0.499999;-0.200002;,
   0.000004;1.000000;-0.200002;,
   0.000004;1.000000;0.100000;,
   0.500008;0.499999;-0.200002;,
   0.500008;1.000000;-0.200002;,
   0.000004;1.000000;-0.200002;,
   0.500000;1.000000;0.100000;,
   0.500008;1.000000;-0.200002;,
   0.000004;1.000000;-0.200002;,
   0.500000;0.500007;0.100000;,
   0.000004;1.000000;0.100000;,
   0.500008;0.499999;-0.200002;;
   7;
   3;0,3,4;,
   3;1,6,7;,
   3;6,1,2;,
   3;8,9,10;,
   3;9,8,5;,
   3;11,12,13;,
   3;12,11,14;;

   MeshNormals  {
    8;
    0.000000;0.000000;-1.000000;,
    1.000000;0.000000;0.000025;,
    1.000000;0.000000;0.000025;,
    1.000000;0.000000;0.000025;,
    1.000000;0.000000;0.000025;,
    0.000000;1.000000;0.000000;,
    -0.707105;-0.707108;-0.000000;,
    -0.707105;-0.707109;-0.000001;;
    7;
    3;0,0,0;,
    3;1,2,3;,
    3;2,1,4;,
    3;5,5,5;,
    3;5,5,5;,
    3;6,6,6;,
    3;7,7,7;;
   }

   MeshMaterialList  {
    1;
    7;
    0,
    0,
    0,
    0,
    0,
    0,
    0;
    { PDX01_-_Default }
   }

   MeshTextureCoords  {
    15;
    0.785281;0.446154;,
    0.694986;0.673496;,
    0.778044;0.673496;,
    0.785281;0.490344;,
    0.739074;0.446154;,
    0.777734;0.673483;,
    0.778045;0.633652;,
    0.694986;0.633652;,
    0.777737;0.633923;,
    0.860830;0.673483;,
    0.860847;0.633923;,
    0.777184;0.633367;,
    0.695020;0.671544;,
    0.777365;0.671531;,
    0.695040;0.633199;;
   }
  }
 }
}