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
  "Egypt_texturepage.bmp";
 }
}

Material sphinx_body {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "sphinx_tex_gen.bmp";
 }
}

Frame tile_boundary_edge {
 

 FrameTransformMatrix {
  1.000000,0.000000,-0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000000,-0.100000,0.000000,1.000000;;
 }

 Frame {
  

  FrameTransformMatrix {
   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,-0.500000,0.000000,1.000000;;
  }

  Mesh  {
   20;
   -0.500000;0.000000;-0.245000;,
   -0.500000;0.000000;0.100000;,
   -0.500000;1.000000;-0.245000;,
   0.000018;0.999982;-0.245000;,
   -0.500000;1.000000;0.100000;,
   0.000003;0.999997;0.100000;,
   0.000003;-0.000000;0.100000;,
   0.000003;-0.000000;-0.245000;,
   -0.500000;1.000000;0.100000;,
   -0.500000;1.000000;-0.245000;,
   0.000018;0.999982;-0.245000;,
   0.000003;0.999997;0.100000;,
   0.000018;0.999982;-0.245000;,
   -0.500000;0.000000;-0.245000;,
   -0.500000;1.000000;-0.245000;,
   0.000003;-0.000000;-0.245000;,
   0.000003;-0.000000;0.100000;,
   -0.500000;0.000000;-0.245000;,
   0.000003;-0.000000;-0.245000;,
   -0.500000;0.000000;0.100000;;
   10;
   3;1,2,0;,
   3;2,1,4;,
   3;3,8,5;,
   3;8,3,9;,
   3;10,6,7;,
   3;6,10,11;,
   3;12,13,14;,
   3;13,12,15;,
   3;16,17,18;,
   3;17,16,19;;

   MeshNormals  {
    11;
    -1.000000;0.000000;0.000000;,
    0.000005;1.000000;-0.000045;,
    0.000036;1.000000;-0.000000;,
    1.000000;-0.000003;0.000033;,
    1.000000;-0.000011;0.000009;,
    1.000000;-0.000014;0.000000;,
    1.000000;-0.000000;0.000041;,
    0.000000;0.000000;-1.000000;,
    -0.000000;0.000000;-1.000000;,
    -0.000000;-1.000000;0.000000;,
    -0.000000;-1.000000;-0.000000;;
    10;
    3;0,0,0;,
    3;0,0,0;,
    3;1,1,1;,
    3;2,2,2;,
    3;3,4,5;,
    3;4,3,6;,
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
    0.491547;0.192339;,
    0.491547;0.228998;,
    0.341227;0.192339;,
    0.493035;0.192610;,
    0.341227;0.228998;,
    0.493018;0.228985;,
    0.483312;0.228808;,
    0.483312;0.193250;,
    0.341088;0.228985;,
    0.341071;0.192610;,
    0.372882;0.193250;,
    0.372882;0.228545;,
    0.128375;0.446154;,
    0.012752;0.556726;,
    0.012752;0.446154;,
    0.082168;0.556726;,
    0.417303;0.228985;,
    0.341071;0.192610;,
    0.417306;0.192610;,
    0.341088;0.228985;;
   }
  }
 }
}