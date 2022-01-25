#include "MainShared.hlsl"
#include "Utils.hlsl"

#define TEST_LINEARIZATION 0

float3 main( OutputVS input ) : SV_TARGET
{
#if TEST_LINEARIZATION
	return 0.005 * input.position.w;
#endif
	return Magic( input );
}
