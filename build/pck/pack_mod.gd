extends SceneTree
## Packs every file under <assets_dir> into <out.pck> at res://<mod_id>/<relative path>.
## Usage: godot --headless --path build/pck -s res://pack_mod.gd -- <assets_dir> <mod_id> <out.pck>


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 3:
		push_error("usage: -- <assets_dir> <mod_id> <out.pck>")
		quit(2)
		return

	var assets_dir: String = args[0].replace("\\", "/")
	var mod_id: String = args[1]
	var out_pck: String = args[2].replace("\\", "/")

	var packer := PCKPacker.new()
	var err := packer.pck_start(out_pck)
	if err != OK:
		push_error("pck_start failed (%s): %s" % [err, out_pck])
		quit(1)
		return

	var count := [0]
	err = _add_dir(packer, assets_dir, assets_dir, mod_id, count)
	if err != OK:
		quit(1)
		return

	err = packer.flush(false)
	if err != OK:
		push_error("flush failed: %s" % err)
		quit(1)
		return

	print("Packed %d file(s) into %s" % [count[0], out_pck])
	quit(0)


func _add_dir(packer: PCKPacker, root: String, dir_path: String, mod_id: String, count: Array) -> int:
	var dir := DirAccess.open(dir_path)
	if dir == null:
		# An empty/missing asset folder still produces a valid (empty) pack.
		return OK

	for sub in dir.get_directories():
		var err := _add_dir(packer, root, dir_path.path_join(sub), mod_id, count)
		if err != OK:
			return err

	for file in dir.get_files():
		var source := dir_path.path_join(file)
		var relative := source.substr(root.length()).trim_prefix("/")
		var target := "res://%s/%s" % [mod_id, relative]
		var err := packer.add_file(target, source)
		if err != OK:
			push_error("add_file failed (%s): %s -> %s" % [err, source, target])
			return err
		count[0] += 1

	return OK
