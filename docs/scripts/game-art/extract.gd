# Pulls files, textures and material values out of the game's PCK. Run by scripts/extract-game-art.mjs; see there.
#
# Arguments (after --): <game.pck> <manifest.json> <output dir>
# The manifest may have:
#   files:     { "<output path>": "res://path" }                           copied as they are (text)
#   textures:  { "<output path, no extension>": { paths: [res:// paths to try, in order], optional: bool } }
#              saved as PNG; atlas regions are cropped, with their margins kept
#   materials: { "<name>": "res://path" } and materialsOut: "<output path>"  each material's hsv shader parameters
extends SceneTree

var _atlases := {}


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 3:
		printerr("usage: extract.gd -- <game.pck> <manifest.json> <output dir>")
		quit(2)
		return

	var pck := args[0]
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
	var out := args[2]
	if not ProjectSettings.load_resource_pack(pck, false):
		printerr("can't load ", pck)
		quit(1)
		return

	var failed := 0

	var files: Dictionary = manifest.get("files", {})
	for name in files:
		if not FileAccess.file_exists(files[name]):
			printerr("missing file ", files[name])
			failed += 1
			continue
		_write(out.path_join(name), FileAccess.get_file_as_string(files[name]))
		print("file ", name, " <- ", files[name])

	var textures: Dictionary = manifest.get("textures", {})
	for name in textures:
		var wanted: Dictionary = textures[name]
		var path := _first_existing(wanted.paths)
		if path.is_empty():
			if wanted.get("optional", false):
				print("no texture for ", name)
			else:
				printerr("missing texture ", name, ": none of ", wanted.paths)
				failed += 1
			continue
		var image := _image_of(load(path))
		if image == null or image.is_empty():
			printerr("can't read ", path)
			failed += 1
			continue
		var target := out.path_join(name + ".png")
		DirAccess.make_dir_recursive_absolute(target.get_base_dir())
		image.save_png(target)
		print("texture ", name, " <- ", path, " ", image.get_size())

	var materials := {}
	var wanted_materials: Dictionary = manifest.get("materials", {})
	for name in wanted_materials:
		var path: String = wanted_materials[name]
		if not ResourceLoader.exists(path):
			printerr("missing material ", path)
			failed += 1
			continue
		var material := load(path) as ShaderMaterial
		materials[name] = {
			"h": material.get_shader_parameter("h"),
			"s": material.get_shader_parameter("s"),
			"v": material.get_shader_parameter("v"),
		}
		print("material ", name, " ", materials[name])
	if manifest.has("materialsOut"):
		_write(out.path_join(manifest.materialsOut), JSON.stringify(materials, "\t", true) + "\n")

	quit(1 if failed > 0 else 0)


func _write(path: String, text: String) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()


func _first_existing(paths: Array) -> String:
	for path in paths:
		if ResourceLoader.exists(path):
			return path
	return ""


## The texture's pixels. Atlas regions are cropped out of their (decompressed) atlas, with the region's margin added
## back as transparent padding, so every piece keeps the size the game lays it out at.
func _image_of(texture: Texture2D) -> Image:
	if texture is AtlasTexture:
		var atlas_texture := texture as AtlasTexture
		var atlas := _decompressed(atlas_texture.atlas)
		var region := Rect2i(atlas_texture.region)
		var piece := atlas.get_region(region)
		var margin := Rect2i(atlas_texture.margin)
		if margin.size == Vector2i.ZERO:
			return piece
		var full := Image.create_empty(region.size.x + margin.size.x, region.size.y + margin.size.y, false, piece.get_format())
		full.blit_rect(piece, Rect2i(Vector2i.ZERO, region.size), margin.position)
		return full
	return _decompressed(texture)


func _decompressed(texture: Texture2D) -> Image:
	var key := texture.resource_path
	if _atlases.has(key):
		return _atlases[key]
	var image := texture.get_image()
	if image != null and image.is_compressed():
		image.decompress()
	if image != null:
		image.convert(Image.FORMAT_RGBA8)
	_atlases[key] = image
	return image
