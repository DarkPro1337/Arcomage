;; Wasmtime host-contract smoke module.
;; Build: wat2wasm smoke.wat -o smoke.wasm
;; Pack:  zip smoke.arcpak metadata.yaml smoke.wasm
;; Copy smoke.arcpak into Godot user://mods/ and look for "smoke :: wasmtime-smoke" at startup.

(module
  (import "env" "host_log" (func $host_log (param i32 i32)))
  (import "env" "abort" (func $abort (param i32 i32 i32 i32)))
  (memory (export "memory") 1)
  (data (i32.const 16) "wasmtime-smoke")
  (func (export "init")
    (call $host_log (i32.const 16) (i32.const 14)))
  (func (export "process") (param $delta f64))
  (func (export "exit"))
)
