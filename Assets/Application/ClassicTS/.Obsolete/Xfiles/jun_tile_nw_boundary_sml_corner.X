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

Frame boundary_small_corner {
 

 FrameTransformMatrix {
  -0.000000,0.000000,1.000000,0.000000,-1.000000,-0.000000,-0.000000,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000003,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,-0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   15;
   0.500008;1.000000;-0.294987;,
   0.500000;1.000000;0.100000;,
   0.500000;0.500007;0.100000;,
   0.500008;0.499999;-0.294987;,
   0.000004;1.000000;-0.294987;,
   0.000004;1.000000;0.100000;,
   0.500008;0.499999;-0.294987;,
   0.500008;1.000000;-0.294987;,
   0.000004;1.000000;-0.294987;,
   0.500000;1.000000;0.100000;,
   0.500008;1.000000;-0.294987;,
   0.000004;1.000000;-0.294987;,
   0.500000;0.500007;0.100000;,
   0.000004;1.000000;0.100000;,
   0.500008;0.499999;-0.294987;;
   7;
   3;0,3,4;,
   3;1,6,7;,
   3;6,1,2;,
   3;8,9,10;,
   3;9,8,5;,
   3;11,12,13;,
   3;12,11,14;;

   MeshNormals  {
    9;
    0.000000;-0.000000;-1.000000;,
    1.000000;0.000000;0.000019;,
    1.000000;0.000000;0.000019;,
    1.000000;0.000000;0.000019;,
    1.000000;0.000000;0.000019;,
    0.000000;1.000000;-0.000000;,
    0.000000;1.000000;-0.000000;,
    -0.707105;-0.707108;-0.000000;,
    -0.707105;-0.707109;-0.000000;;
    7;
    3;0,0,0;,
    3;1,2,3;,
    3;2,1,4;,
    3;5,5,5;,
    3;6,6,6;,
    3;7,7,7;,
    3;8,8,8;;
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
    0.128973;0.306690;,
    0.026308;0.290588;,
    0.093003;0.290588;,
    0.128973;0.350879;,
    0.082167;0.306690;,
    0.027039;0.290574;,
    0.093004;0.263558;,
    0.026308;0.263558;,
    0.027042;0.263830;,
    0.092064;0.290574;,
    0.092080;0.263830;,
    0.092826;0.263708;,
    0.025984;0.290588;,
    0.092502;0.290574;,
    0.026004;0.263541;;
   }
  }
 }
}