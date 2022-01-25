#include "MainShared.hlsl"
#include "Utils.hlsl"

struct InputVS
{
	float4 position : POSITION;
};

float Hash12n( float2 p )
{
	p = frac( p * float2( 5.3987, 5.4421 ) );
	p += dot( p.yx, p.xy + float2( 21.5351, 14.3137 ) );
	return frac( p.x * p.y * 95.4307 );
}

OutputVS main( InputVS input )
{
	float4 worldPosition = mul(worldMatrix, input.position);
	float2 origin = float2(0.0, 0.0);
	float distance = length(worldPosition.xy - origin) + 2;

	float a = 40 / distance;

	//ripple form
	worldPosition.z += a * Hash12n(worldPosition.xy);

	OutputVS output;
	output.worldPosition = worldPosition;
	output.position = mul(viewProjectionMatrix, worldPosition);

	return output;
}
