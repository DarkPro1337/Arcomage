local nk = require("nakama")

nk.run_once(function(_)
  pcall(function()
    nk.leaderboard_create("arcomage_rating", false, "desc", "set")
  end)
end)

local function decode_payload(payload)
  if payload == nil or payload == "" then
    return {}
  end
  local ok, decoded = pcall(nk.json_decode, payload)
  if ok and type(decoded) == "table" then
    return decoded
  end
  return {}
end

local function sanitize_code(code)
  code = string.upper(tostring(code or ""))
  return string.gsub(code, "[^A-Z0-9]", "")
end

local function user_properties(user)
  if type(user) ~= "table" then
    return {}
  end
  if type(user.properties) == "table" then
    return user.properties
  end
  if type(user.string_properties) == "table" then
    return user.string_properties
  end
  return {}
end

local function rpc_create_match(_, payload)
  local params = decode_payload(payload)
  local kind = tostring(params.kind or "matchmaker")
  local mode = tostring(params.mode or "")
  local code = sanitize_code(params.code)
  local ranked = params.ranked == true
  local match_id = nk.match_create("arcomage_match", {
    kind = kind,
    mode = mode,
    code = code,
    ranked = ranked
  })
  return nk.json_encode({ match_id = match_id })
end

local function rpc_find_room(_, payload)
  local params = decode_payload(payload)
  local code = sanitize_code(params.code)
  if code == "" then
    error("Room code is required")
  end

  local matches = nk.match_list(100, true, nil, 0, 100, nil)
  if type(matches) == "table" then
    for _, match in ipairs(matches) do
      local ok, label = pcall(nk.json_decode, match.label or "")
      if ok and type(label) == "table" and label.kind == "room" and sanitize_code(label.code) == code then
        return nk.json_encode({ match_id = match.match_id })
      end
    end
  end

  error("Room not found")
end

local function matchmaker_matched(_, matched_users)
  local props = {}
  if type(matched_users) == "table" and #matched_users > 0 then
    props = user_properties(matched_users[1])
  end

  local mode = tostring(props.mode or "OneVsOne")
  local queue = tostring(props.queue or "")
  local ranked = string.find(queue, "ranked_", 1, true) == 1

  return nk.match_create("arcomage_match", {
    kind = "matchmaker",
    mode = mode,
    code = "",
    ranked = ranked
  })
end

nk.register_rpc(rpc_create_match, "create_match")
nk.register_rpc(rpc_find_room, "find_room")
nk.register_matchmaker_matched(matchmaker_matched)
