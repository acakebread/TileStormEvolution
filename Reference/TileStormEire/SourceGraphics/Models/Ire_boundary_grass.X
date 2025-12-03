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
  "Ire_texturepage.tga";
 }
}

Material Material__11_08_-_DefaultSub0 {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Ire_texturepage.tga";
 }
}

Material Material__11_08_-_DefaultdtSub1 {
 0.023529;0.533333;0.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;
}

Material gate {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Ire_gate_01.tga";
 }
}

Frame boundary25 {
 

 FrameTransformMatrix {
  1.000000,0.000000,0.000000,0.000000,-0.000000,-0.000000,1.000000,0.000000,0.000000,-1.000000,-0.000000,0.000000,0.000000,-0.099996,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,9.303273,-0.500002,0.000000,1.000000;;
  }

  Mesh  {
   20;
   -8.803269;0.000002;-0.245000;,
   -8.803277;0.000002;0.100000;,
   -8.803277;1.000002;0.100000;,
   -8.803269;1.000002;-0.245000;,
   -9.803277;0.000002;0.100000;,
   -9.803277;0.000002;-0.245000;,
   -9.803277;1.000002;-0.245000;,
   -9.803277;1.000002;0.100000;,
   -8.803269;1.000002;-0.245000;,
   -9.803277;0.000002;-0.245000;,
   -9.803277;1.000002;-0.245000;,
   -8.803269;0.000002;-0.245000;,
   -9.803277;0.000002;-0.245000;,
   -8.803277;0.000002;0.100000;,
   -9.803277;0.000002;0.100000;,
   -8.803269;0.000002;-0.245000;,
   -8.803269;1.000002;-0.245000;,
   -9.803277;1.000002;0.100000;,
   -8.803277;1.000002;0.100000;,
   -9.803277;1.000002;-0.245000;;
   10;
   3;0,2,1;,
   3;2,0,3;,
   3;4,6,5;,
   3;6,4,7;,
   3;8,9,10;,
   3;9,8,11;,
   3;12,13,14;,
   3;13,12,15;,
   3;16,17,18;,
   3;17,16,19;;

   MeshNormals  {
    11;
    1.000000;-0.000000;0.000022;,
    1.000000;-0.000000;0.000022;,
    1.000000;0.000000;0.000022;,
    1.000000;-0.000000;0.000022;,
    -1.000000;0.000000;0.000000;,
    0.000000;0.000000;-1.000000;,
    -0.000000;0.000000;-1.000000;,
    0.000000;-1.000000;0.000000;,
    -0.000000;-1.000000;0.000000;,
    0.000000;1.000000;-0.000000;,
    0.000000;1.000000;0.000000;;
    10;
    3;0,1,2;,
    3;1,0,3;,
    3;4,4,4;,
    3;4,4,4;,
    3;5,5,5;,
    3;6,6,6;,
    3;7,7,7;,
    3;8,8,8;,
    3;9,9,9;,
    3;10,10,10;;
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
    0.788988;0.242522;,
    0.788988;0.289457;,
    0.631593;0.289457;,
    0.631593;0.242522;,
    0.788988;0.289457;,
    0.788988;0.242522;,
    0.631593;0.242522;,
    0.631593;0.289457;,
    0.128019;0.440156;,
    0.008979;0.558870;,
    0.008979;0.440156;,
    0.128019;0.558870;,
    0.631361;0.242926;,
    0.791180;0.289437;,
    0.631386;0.289437;,
    0.791205;0.242926;,
    0.791205;0.242926;,
    0.631386;0.289437;,
    0.791180;0.289437;,
    0.631361;0.242926;;
   }
  }
 }
}