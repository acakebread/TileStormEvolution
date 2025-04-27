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

Material temple_body {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "mayan_temple_tex_gen.tga";
 }
}

Material staircase_base {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "painted stucco.tga";
 }
}

Material stair {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "brick yellow.tga";
 }
}

Frame earth_grass_corner {
 

 FrameTransformMatrix {
  0.000001,0.000000,-1.000000,0.000000,1.000000,0.000000,0.000001,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000000,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   20;
   -0.500000;0.000000;-0.295000;,
   0.500008;-0.000000;-0.295000;,
   -0.500000;0.000000;0.100000;,
   0.500000;0.000000;0.100000;,
   -0.500000;1.000000;-0.295000;,
   0.500008;1.000000;-0.295000;,
   -0.500000;1.000000;0.100000;,
   0.500000;1.000000;0.100000;,
   0.500008;1.000000;-0.295000;,
   -0.500000;0.000000;-0.295000;,
   -0.500000;1.000000;-0.295000;,
   0.500008;-0.000000;-0.295000;,
   -0.500000;0.000000;-0.295000;,
   0.500000;0.000000;0.100000;,
   -0.500000;0.000000;0.100000;,
   0.500008;-0.000000;-0.295000;,
   0.500008;1.000000;-0.295000;,
   -0.500000;1.000000;0.100000;,
   0.500000;1.000000;0.100000;,
   -0.500000;1.000000;-0.295000;;
   10;
   3;1,7,3;,
   3;7,1,5;,
   3;2,4,0;,
   3;4,2,6;,
   3;8,9,10;,
   3;9,8,11;,
   3;12,13,14;,
   3;13,12,15;,
   3;16,17,18;,
   3;17,16,19;;

   MeshNormals  {
    11;
    1.000000;-0.000000;0.000019;,
    1.000000;-0.000000;0.000019;,
    1.000000;0.000000;0.000019;,
    1.000000;-0.000000;0.000019;,
    -1.000000;0.000000;0.000000;,
    0.000000;0.000000;-1.000000;,
    -0.000000;0.000000;-1.000000;,
    0.000000;-1.000000;-0.000000;,
    -0.000000;-1.000000;0.000000;,
    0.000000;1.000000;-0.000000;,
    0.000000;1.000000;-0.000000;;
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
    { PDX01_-_Default }
   }

   MeshTextureCoords  {
    20;
    0.128073;0.262865;,
    0.128073;0.262865;,
    0.128073;0.293260;,
    0.128073;0.293260;,
    0.013106;0.262865;,
    0.013106;0.262865;,
    0.013106;0.293260;,
    0.013106;0.293260;,
    0.527454;0.306307;,
    0.411715;0.425134;,
    0.411715;0.306307;,
    0.527454;0.425134;,
    0.012950;0.263136;,
    0.128046;0.293247;,
    0.012967;0.293247;,
    0.128063;0.263136;,
    0.128063;0.263136;,
    0.012967;0.293247;,
    0.128046;0.293247;,
    0.012950;0.263136;;
   }
  }
 }
}