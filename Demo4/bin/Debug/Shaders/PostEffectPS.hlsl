#include "PostEffectShared.hlsl"
#include "Utils.hlsl"

static const float2 RenderTargetSize = float2( 1280.0, 720.0 );
static const float2 PixelSize = 1.0 / RenderTargetSize; // Size of a pixel in texcoords (UV coordinates)
static const float depthRange = 1;

Texture2D sceneTexture : register( t0 );
Texture2D depthTexture : register( t1 );
SamplerState samplerState : register( s0 );

static const int offsetCount = 24;
static const float2 offsets[] =
{
    { -0.2828, -0.2828 },
    { -0.0000, -0.4000 },
    {  0.2828, -0.2828 },
    {  0.4000,  0.0000 },
    {  0.2828,  0.2828 },
    {  0.0000,  0.4000 },
    { -0.2828,  0.2828 },
    { -0.4000,  0.0000 },
    { -0.7391, -0.3061 },
    { -0.5657, -0.5657 },
    { -0.3061, -0.7391 },
    { -0.0000, -0.8000 },
    {  0.3061, -0.7391 },
    {  0.5657, -0.5657 },
    {  0.7391, -0.3061 },
    {  0.8000,  0.0000 },
    {  0.7391,  0.3061 },
    {  0.5657,  0.5657 },
    {  0.3061,  0.7391 },
    {  0.0000,  0.8000 },
    { -0.3061,  0.7391 },
    { -0.5657,  0.5657 },
    { -0.7391,  0.3061 },
    { -0.8000,  0.0000 }
};


float3 main( OutputVS input ) : SV_TARGET
{
    //read texture and return its color and depth
    float3 color = sceneTexture.Sample(samplerState, input.texcoord).rgb; //input.textcoord is current pixel being processed
    float currentPixelDepth = depthTexture.Sample(samplerState, input.texcoord).r;

    //DoF YZW
    float isActive = parameters.y;
    float intensity = parameters.z; //?????
    float depth = parameters.w;

    float DoF = 1.0f; //init
    float2 currentPos; //current pixel position from center
    float currentDepth; //depth of pixel
    float visibility; //checks depth regarding its surroundings


    //apply ambient occlusion if true
    if (isActive != 0.0f)
    {
        for (int i = 0; i < offsetCount; ++i)
        {
            color += sceneTexture.Sample(samplerState, input.texcoord + PixelSize * offsets[i]).rgb;
        }
        /*for (int i = 0; i < offsetCount; ++i)
        {
            //get the pixel neightbors in the iteration and its depth
            for (int j = -10; j < 10; j++)
            {
                for (int k = -10; k < 10; k++)
                {
                    //float chechDepth = depthTexture.Sample(samplerState, input.texcoord + PixelSize * float2(j, k)).r;

                    if (abs(depth - chechDepth) < depthRange)
                    {
                        color += sceneTexture.Sample(samplerState, input.texcoord + PixelSize * float2(j, k)).rgb;
                    }
                }
            }
        }*/

        color /= 25;
    }


    return color;
}
