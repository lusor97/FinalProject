cbuffer ConstantBuffer : register(b0)
{
	float4 parameters; // .x has time
}

struct OutputVS
{
	float4 position : SV_POSITION;
	float2 texcoord : TEXCOORD;
};
