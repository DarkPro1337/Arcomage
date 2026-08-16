local nk = require("nakama")

nk.run_once(function(_)
  pcall(function()
    nk.leaderboard_create("arcomage_rating", false, "desc", "set")
  end)
end)
