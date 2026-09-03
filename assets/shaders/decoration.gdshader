shader_type spatial;
render_mode cull_back, diffuse_burley;

// Clutter (grass tufts, loose stones). Uses the same fog texture as the terrain,
// so scattered objects do not shine through in unexplored territory.
//
// The UV is computed from the world position, because MultiMesh instances have no
// UV ranges of their own — the map extent comes in as a uniform instead.

uniform vec4 albedo_color : source_color = vec4(0.26, 0.36, 0.17, 1.0);
uniform sampler2D fog_map : filter_linear, hint_default_white;
uniform float fog_darkness = 0.82;
uniform vec2 map_size = vec2(256.0, 256.0);

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 fog_uv = world_position.xz / map_size + vec2(0.5);
	float visibility = texture(fog_map, fog_uv).r;

	ALBEDO = albedo_color.rgb * mix(1.0 - fog_darkness, 1.0, visibility);
	ROUGHNESS = 0.95;
}
