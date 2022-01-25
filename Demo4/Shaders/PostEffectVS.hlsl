#include "PostEffectShared.hlsl"

struct InputVS
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

OutputVS main( InputVS input )
{
	OutputVS output;
	output.position = input.position;
	output.texcoord = input.texcoord;
	return output;
}
