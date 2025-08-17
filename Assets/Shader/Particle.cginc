#ifndef _GPU_PARTICLE_
#define _GPU_PARTICLE_

struct Particle {
	float4 pos;
	float3 velocity;
	float mass;
	float lifeTime;
	float radius;
};

#endif