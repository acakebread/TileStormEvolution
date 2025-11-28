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
  "Jungle_texturepage.tga";
 }
}

Material Standard_1 {
 0.588000;0.588000;0.588000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;
}

Material Material__11_temple_bodySub0 {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "mayan_temple_tex_gen.tga";
 }
}

Material Material__11_01_-_DefaultSub1 {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Jungle_texturepage.tga";
 }
}

Frame tile_boundary_edged_half {
 

 FrameTransformMatrix {
  -0.000000,-0.000000,1.000000,0.000000,-1.000000,-0.000000,-0.000000,0.000000,0.000000,-1.000000,-0.000000,0.000000,0.000003,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,-0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   20;
   -0.500000;0.000000;-0.294997;,
   0.500008;0.000000;-0.294996;,
   -0.500000;0.000000;0.100000;,
   0.500000;0.000000;0.100000;,
   -0.500000;0.495956;-0.294997;,
   -0.500000;0.495956;0.100000;,
   0.500000;0.499998;0.100000;,
   0.500008;0.499990;-0.294996;,
   -0.500000;0.000000;-0.294997;,
   -0.500000;0.000000;0.100000;,
   0.500000;0.000000;0.100000;,
   0.500008;0.000000;-0.294996;,
   -0.500000;0.000000;-0.294997;,
   0.500008;0.499990;-0.294996;,
   0.500008;0.000000;-0.294996;,
   -0.500000;0.495956;-0.294997;,
   -0.500000;0.495956;-0.294997;,
   0.500000;0.499998;0.100000;,
   0.500008;0.499990;-0.294996;,
   -0.500000;0.495956;0.100000;;
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
    14;
    -1.000000;-0.000000;0.000000;,
    -1.000000;-0.000000;0.000000;,
    -1.000000;-0.000000;0.000000;,
    -1.000000;-0.000000;0.000000;,
    -0.000000;-1.000000;-0.000000;,
    0.000000;-1.000000;-0.000000;,
    1.000000;-0.000000;0.000019;,
    1.000000;-0.000000;0.000019;,
    1.000000;-0.000000;0.000019;,
    1.000000;0.000000;0.000019;,
    0.000000;-0.000000;-1.000000;,
    0.000000;0.000000;-1.000000;,
    -0.004034;0.999992;-0.000019;,
    -0.004041;0.999992;0.000000;;
    10;
    3;0,1,2;,
    3;1,0,3;,
    3;4,4,4;,
    3;5,5,5;,
    3;6,7,8;,
    3;7,6,9;,
    3;10,10,10;,
    3;11,11,11;,
    3;12,12,12;,
    3;13,13,13;;
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
    0.093875;0.261948;,
    0.116559;0.261714;,
    0.093875;0.292107;,
    0.116546;0.292599;,
    0.048697;0.261948;,
    0.048697;0.292107;,
    0.079345;0.292107;,
    0.079346;0.261948;,
    0.015066;0.261714;,
    0.015078;0.292599;,
    0.117280;0.292107;,
    0.117280;0.261948;,
    0.128375;0.423041;,
    0.012752;0.355881;,
    0.012752;0.423041;,
    0.128259;0.355744;,
    0.021372;0.262357;,
    0.121735;0.291564;,
    0.121737;0.262357;,
    0.021371;0.291564;;
   }
  }
 }
}