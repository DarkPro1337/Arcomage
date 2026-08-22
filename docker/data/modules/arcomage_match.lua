local nk = require("nakama")

local M = {}
local OpChat = 4
local MaxChat = 20
local TickRate = 30
local HistoryCollection = "match_history"
local HistoryOwner = "00000000-0000-0000-0000-000000000000"

local function presence_count(state)
  local n = 0
  for _ in pairs(state.presences) do
    n = n + 1
  end
  return n
end

local function player_list(state)
  local players = {}
  if type(state.players) ~= "table" then
    return players
  end
  for _, player in pairs(state.players) do
    players[#players + 1] = player
  end
  return players
end

local function persist_history(state, status)
  if state == nil or state.match_id == nil or state.match_id == "" then
    return
  end

  local record = {
    match_id = state.match_id,
    kind = state.kind or "",
    mode = state.mode or "",
    code = state.code or "",
    ranked = state.ranked == true,
    status = status,
    started_at = state.started_at,
    ended_at = status == "ended" and os.time() or nil,
    size = presence_count(state),
    players = player_list(state),
    chat = state.chat
  }

  pcall(nk.storage_write, {
    {
      collection = HistoryCollection,
      key = state.match_id,
      user_id = HistoryOwner,
      value = record,
      permission_read = 2,
      permission_write = 0
    }
  })
end

local function match_label(state)
  return nk.json_encode({
    kind = state.kind or "",
    mode = state.mode or "",
    code = state.code or "",
    ranked = state.ranked == true,
    size = presence_count(state)
  })
end

local function update_label(dispatcher, state)
  dispatcher.match_label_update(match_label(state))
end

local function remember_presence(state, presence)
  local snapshot = {
    user_id = presence.user_id,
    username = presence.username or "",
    session_id = presence.session_id
  }
  state.presences[presence.session_id] = snapshot
  if presence.user_id ~= nil and presence.user_id ~= "" then
    state.players = state.players or {}
    state.players[presence.user_id] = {
      user_id = presence.user_id,
      username = presence.username or ""
    }
  end
end

local function append_chat(state, message)
  local text = message.data or ""
  local username = ""
  if message.sender ~= nil then
    username = message.sender.username or ""
  end
  state.chat[#state.chat + 1] = {
    username = username,
    text = text,
    t = os.time()
  }
  while #state.chat > MaxChat do
    table.remove(state.chat, 1)
  end
end

function M.match_init(context, params)
  params = params or {}
  local state = {
    match_id = context.match_id or "",
    kind = params.kind or "matchmaker",
    mode = params.mode or "",
    code = params.code or "",
    ranked = params.ranked == true,
    started_at = os.time(),
    presences = {},
    players = {},
    chat = {}
  }
  persist_history(state, "open")
  return state, TickRate, match_label(state)
end

function M.match_join_attempt(_, _, _, state, _, _)
  return state, true
end

function M.match_join(_, dispatcher, _, state, presences)
  for _, presence in ipairs(presences) do
    remember_presence(state, presence)
  end
  update_label(dispatcher, state)
  persist_history(state, "live")
  return state
end

function M.match_leave(_, dispatcher, _, state, presences)
  for _, presence in ipairs(presences) do
    state.presences[presence.session_id] = nil
  end
  if next(state.presences) == nil then
    persist_history(state, "ended")
    return nil
  end
  update_label(dispatcher, state)
  persist_history(state, "live")
  return state
end

function M.match_loop(_, dispatcher, _, state, messages)
  for _, message in ipairs(messages) do
    if message.op_code == OpChat then
      append_chat(state, message)
    end
    dispatcher.broadcast_message(message.op_code, message.data, nil, message.sender)
  end
  return state
end

function M.match_terminate(_, _, _, state, _)
  persist_history(state, "ended")
  return state
end

function M.match_signal(_, _, _, state, data)
  return state, data or ""
end

return M
