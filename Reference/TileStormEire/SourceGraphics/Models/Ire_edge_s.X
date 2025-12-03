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

Material PDX04_-_Default {
 1.000000;1.000000;1.000000;1.000000;;
 3.200000;
 0.000000;0.000000;0.000000;;
 0.000000;0.000000;0.000000;;

 TextureFilename {
  "Ire_tree_01.tga";
 }
}

Frame tile_boundary_edge {
 

 FrameTransformMatrix {
  0.000005,0.000000,1.000000,0.000000,-1.000000,0.000000,0.000005,0.000000,0.000000,-1.000000,0.000000,0.000000,0.000000,-0.100000,0.000000,1.000000;;
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
    0.797835;0.243322;,
    0.777374;0.243730;,
    0.797835;0.289747;,
    0.777346;0.291436;,
    0.630840;0.243322;,
    0.630840;0.289747;,
    0.127641;0.441497;,
    0.694466;0.291436;,
    0.694853;0.243730;,
    0.694881;0.291436;,
    0.068241;0.558279;,
    0.068241;0.441497;,
    0.127404;0.558173;,
    0.777272;0.291436;,
    0.694461;0.243730;,
    0.777300;0.243730;,
    0.799133;0.243778;,
    0.633522;0.291694;,
    0.633521;0.243778;,
    0.799133;0.291694;;
   }
  }
 }
}