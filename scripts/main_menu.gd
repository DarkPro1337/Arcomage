extends Control

onready var settings = $settings
onready var startup = $startupAnim
onready var anim = $menuAnim
onready var info = $info

func _ready():
	get_tree().paused = false

func _on_new_game_pressed():
	anim.play("fade_out")
	yield(anim, "animation_finished")
	get_tree().change_scene("res://scenes/table.tscn")

func _on_settings_pressed():
	settings.show()
	anim.play("settings_show")

func _on_scores_pressed():
	alert("Work in progress...", "Oops")

func _on_exit_pressed():
	get_tree().quit()

func _on_credits_pressed():
	info.show()

func alert(text: String, title: String='Message') -> void:
	var dialog = AcceptDialog.new()
	dialog.dialog_text = text
	dialog.window_title = title
	dialog.connect('modal_closed', dialog, 'queue_free')
	add_child(dialog)
	dialog.popup_centered()
