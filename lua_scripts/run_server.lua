local json = require("json") -- Используйте Lua CJSON или аналогичную библиотеку для парсинга JSON

-- Путь к конфигурационному файлу (абсолютный путь)
local config_path = "D:\\PROTEI\\projects\\current task - ttl and rabbit wit slaves\\client\\lua_configs\\config_server.json"

-- Функция для чтения конфигурации из файла
local function read_config(path)
    local file = io.open(path, "r")
    if not file then
        error("Не удалось открыть файл конфигурации: " .. path)
    end

    local content = file:read("*a")
    file:close()
    return json.decode(content)
end

-- Остальной код остается без изменений
local function run_server(port, reconnect_timer, idle_timer, processing_delay)
    local app_path = "D:\\PROTEI\\projects\\current task - ttl and rabbit wit slaves\\client\\server\\bin\\Debug\\net8.0\\server.dll"

    local command = string.format(
        'dotnet "%s" --port=%s --reconnect-timer=%s --idle-timer=%s --processing-delay=%s',
        app_path,
        port,
        reconnect_timer,
        idle_timer,
        processing_delay
    )

    print("Запуск приложения на порту:", port)
    print("Выполняется команда:", command)

    local result = os.execute(command)
    if result == 0 then
        print("Приложение успешно запущено на порту", port)
    else
        print("Ошибка при запуске приложения")
    end
end

local config = read_config(config_path)

local port = config.Port or 5000
local reconnect_timer = config.ReconnectTimer or 5000
local idle_timer = config.IdleTimer or 15000
local processing_delay = config.ProcessingDelay or 10000

run_server(port, reconnect_timer, idle_timer, processing_delay)
