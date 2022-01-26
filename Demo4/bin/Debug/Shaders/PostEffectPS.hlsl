#include "PostEffectShared.hlsl"
#include "Utils.hlsl"

static const float2 RenderTargetSize = float2( 1280.0, 720.0 );
static const float2 PixelSize = 1.0 / RenderTargetSize; // Size of a pixel in texcoords (UV coordinates)

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
    //read pixel and return its color and depth
    float3 color = sceneTexture.Sample(samplerState, input.texcoord).rgb;
    float currentPixelDepth = depthTexture.Sample(samplerState, input.texcoord).r;

    //DoF YZW
    float isFixed = parameters.y;
    float isVariable = parameters.z;
    float depth = parameters.w;

    //variables
    float near = 1;
    float far = 1000;
    float depthRange = 3.0f;

    //apply DoF Fixed
    if (isFixed != 0.0f)
    {
        for (int i = 0; i < offsetCount; ++i)
        {
            color += sceneTexture.Sample(samplerState, input.texcoord + PixelSize * (offsets[i] * 5)).rgb;
        }

        color /= 25;
    }
    //apply DoF Variable
    if (isVariable != 0.0f)
    {
        float normalizedDepth = ((near * far) / (far - currentPixelDepth * far + near * currentPixelDepth));

        if (abs(depth - normalizedDepth) > depthRange)
        {
            [unroll]
            for (int i = 0; i < offsetCount; ++i)
            {
                float intensity = (abs(depth - normalizedDepth) - depthRange) * 0.3f;
                if (intensity > 5)
                {
                    intensity = 5;
                }
                color += sceneTexture.Sample(samplerState, input.texcoord + PixelSize * (offsets[i] * intensity)).rgb;
            }

            color /= 25;
        }
    }
    return color;
}
