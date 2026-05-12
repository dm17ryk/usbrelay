-- Clink completions for usbrelay.
-- Optional: set USBRELAY_COMPLETION_COMMAND to the full path of usbrelay.exe.

local usbrelay_completion = clink.generator(50)

local function basename(value)
    value = tostring(value or ""):gsub('"', "")
    return (value:match("[^\\/:]+$") or value):lower()
end

local function is_usbrelay_command(line_state)
    local command_index = line_state:getcommandwordindex()
    if not command_index or command_index < 1 then
        return false
    end

    local command = basename(line_state:getword(command_index))
    return command == "usbrelay" or command == "usbrelay.exe"
end

local function quote_arg(value)
    value = tostring(value or "")
    value = value:gsub('"', '\\"')
    return '"' .. value .. '"'
end

function usbrelay_completion:generate(line_state, match_builder)
    if not is_usbrelay_command(line_state) then
        return false
    end

    local executable = os.getenv("USBRELAY_COMPLETION_COMMAND") or "usbrelay.exe"
    local line = line_state:getline()
    local position = line_state:getcursor() - 1
    local command = quote_arg(executable) .. " complete --position " .. tostring(position) .. " --line " .. quote_arg(line)
    local handle = io.popen(command .. " 2>nul")
    if not handle then
        return false
    end

    local count = 0
    for completion in handle:lines() do
        if completion ~= "" then
            match_builder:addmatch(completion)
            count = count + 1
        end
    end
    handle:close()

    return count > 0
end
