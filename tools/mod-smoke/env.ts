@external("env", "host_log")
declare function host_log(ptr: i32, len: i32): void;

@external("env", "abort")
declare function abort(msg: i32, file: i32, line: i32, column: i32): void;

export function log(message: string): void {
  const encoded = String.UTF8.encode(message);
  host_log(changetype<i32>(encoded), encoded.byteLength);
}
