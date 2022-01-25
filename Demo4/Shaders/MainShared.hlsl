cbuffer ConstantBuffer : register( b0 )
{
	float4x4 worldMatrix;
	float4x4 viewProjectionMatrix;
	float4x4 worldViewProjectionMatrix;
	float time; // in seconds
}

struct OutputVS
{
	float4 position : SV_POSITION;
	float4 worldPosition : TEXCOORD0;
};

float3 Magic( OutputVS input )
{
	// WARNING: don't do this at home homies
	float3 normal = normalize( cross( ddx( input.worldPosition.xyz ), ddy( input.worldPosition.xyz ) ) );
	float3 lightDir = normalize( float3( 1.0, 0.0, -2.0 ) );
	return saturate( dot( normal, float3( lightDir ) ) ) * 0.75 * float3( 0.85, 0.85, 0.72 ) + 0.75f * float3( 0.25, 0.35, 0.42 );
}
