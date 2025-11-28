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

Frame tile_boundary_edged_corner01 {
 

 FrameTransformMatrix {
  -0.000001,0.000000,1.000000,0.000000,-1.000000,0.000000,-0.000001,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000003,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   20;
   -0.500000;0.000000;-0.199994;,
   0.500008;-0.000000;-0.199994;,
   -0.500000;0.000000;0.100000;,
   0.500000;0.000000;0.100000;,
   -0.500000;0.500003;-0.199994;,
   -0.500000;0.500003;0.100000;,
   0.500000;0.499998;0.100000;,
   0.500008;0.499990;-0.199994;,
   -0.500000;0.000000;-0.199994;,
   -0.500000;0.000000;0.100000;,
   0.500000;0.000000;0.100000;,
   0.500008;-0.000000;-0.199994;,
   -0.500000;0.000000;-0.199994;,
   0.500008;0.499990;-0.199994;,
   0.500008;-0.000000;-0.199994;,
   -0.500000;0.500003;-0.199994;,
   -0.500000;0.500003;-0.199994;,
   0.500000;0.499998;0.100000;,
   0.500008;0.499990;-0.199994;,
   -0.500000;0.500003;0.100000;;
   10;
   3;4,2,5;,
   3;2,4,0;,
   3;3,8,1;,
   3;8,3,9;,
   3;7,10,11;,
   3;10,7,6;,
   3;12,13,14;,
   3;15,13,12;,
   3;16,17,18;,
   3;16,19,17;;

   MeshNormals  {
    12;
    -1.000000;-0.000000;0.000000;,
    -1.000000;-0.000000;0.000000;,
    -0.000000;-1.000000;0.000000;,
    0.000000;-1.000000;0.000000;,
    1.000000;-0.000000;0.000025;,
    1.000000;-0.000000;0.000025;,
    1.000000;-0.000000;0.000025;,
    1.000000;0.000000;0.000025;,
    -0.000000;0.000000;-1.000000;,
    -0.000000;0.000000;-1.000000;,
    0.000013;1.000000;-0.000025;,
    0.000006;1.000000;-0.000000;;
    10;
    3;0,1,0;,
    3;1,0,0;,
    3;2,2,2;,
    3;3,3,3;,
    3;4,5,6;,
    3;5,4,7;,
    3;8,8,8;,
    3;9,9,9;,
    3;10,10,10;,
    3;11,11,11;;
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
    { PDX01_-_Default }
   }

   MeshTextureCoords  {
    20;
    0.777393;0.633577;,
    0.860380;0.633848;,
    0.777393;0.673421;,
    0.860364;0.673408;,
    0.696759;0.633577;,
    0.696759;0.673421;,
    0.695111;0.673421;,
    0.695112;0.633577;,
    0.696537;0.633848;,
    0.696554;0.673408;,
    0.777740;0.673421;,
    0.777740;0.633577;,
    0.671896;0.556726;,
    0.786828;0.490344;,
    0.786828;0.556726;,
    0.672070;0.490234;,
    0.695839;0.633986;,
    0.859526;0.672234;,
    0.859528;0.633986;,
    0.695839;0.672234;;
   }
  }
 }
}