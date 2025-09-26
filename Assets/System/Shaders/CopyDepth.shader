Shader "Hidden/CopyDepth"
{
    SubShader
    {
        Pass
        {
            ZWrite On
            ZTest Always
            ColorMask 0
        }
    }
}
