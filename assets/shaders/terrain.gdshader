shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

// Four-way splatting. The weights sit in the channels of the splat map:
// R = Gras, G = Erde, B = Fels, A = Sand.
//
// While no real textures exist, the samplers default to white and only the
// colour values remain. As soon as textures are assigned they modulate those
// colours — without any change to this shader.

uniform sampler2D splat_map : filter_linear, hint_default_black;

uniform sampler2D tex_grass : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_dirt  : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_rock  : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_sand  : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;

uniform vec4 color_grass : source_color = vec4(0.29, 0.40, 0.20, 1.0);
uniform vec4 color_dirt  : source_color = vec4(0.36, 0.29, 0.20, 1.0);
uniform vec4 color_rock  : source_color = vec4(0.42, 0.41, 0.39, 1.0);
uniform vec4 color_sand  : source_color = vec4(0.61, 0.56, 0.40, 1.0);

uniform float tile_scale = 0.12;
uniform float detail_strength = 0.10;
uniform float terrain_roughness = 0.93;

// Fog of war: 1 = visible, ~0.45 = remembered, 0 = unexplored.
// Defaults to white, so the terrain stays fully visible without an assigned texture.
uniform sampler2D fog_map : filter_linear, hint_default_white;
uniform float fog_darkness = 0.82;

varying vec3 world_position;

// Cheap value noise that breaks up the large flat areas of colour.
// Can be removed outright once real textures are in.
float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);

	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));

	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec4 weights = texture(splat_map, UV);

	// Normalise, so brightness stays independent of the sum of the weights.
	float total = weights.r + weights.g + weights.b + weights.a;
	weights /= max(total, 0.0001);

	vec2 tile_uv = world_position.xz * tile_scale;

	vec3 albedo =
		texture(tex_grass, tile_uv).rgb * color_grass.rgb * weights.r +
		texture(tex_dirt,  tile_uv).rgb * color_dirt.rgb  * weights.g +
		texture(tex_rock,  tile_uv).rgb * color_rock.rgb  * weights.b +
		texture(tex_sand,  tile_uv).rgb * color_sand.rgb  * weights.a;

	// Two noise frequencies layered: coarse for patches, fine for grain.
	float detail =
		value_noise(world_position.xz * 0.35) * 0.65 +
		value_noise(world_position.xz * 1.70) * 0.35;

	albedo *= 1.0 - detail_strength + detail * detail_strength * 2.0;

	// Unexplored terrain goes almost black, remembered terrain clearly dimmed.
	float visibility = texture(fog_map, UV).r;
	albedo *= mix(1.0 - fog_darkness, 1.0, visibility);

	ALBEDO = albedo;
	ROUGHNESS = terrain_roughness;
	SPECULAR = 0.15;
}
