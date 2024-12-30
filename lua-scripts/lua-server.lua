-- lua_script.lua

-- Чтение конфигурации из JSON файла
local json = require("json")  -- Убедитесь, что в Lua доступна библиотека JSON
local config_file = "C:\\test-integration\\client-rabbit-ttl-server\\lua-configs\\config.json"

-- Чтение файла конфигурации
local config_data = nil
local file = io.open(config_file, "r")
if file then
    local content = file:read("*all")
    file:close()
    config_data = json.decode(content)  -- Декодируем JSON в таблицу Lua
else
    print("Не удалось открыть файл конфигурации.")
    return
end

-- Параметры из конфигурации
local processing_time = config_data.processing_time or 10000
local idle_timeout = config_data.idle_timeout or 15000
local reconnect_interval = config_data.reconnect_interval or 5000

-- Путь к C# приложению (обновите путь до вашего исполняемого файла)
local app_path = "C:\\test-integration\\client-rabbit-ttl-server\\server\\bin\\Debug\\net8.0\\server.exe"

-- Команда для запуска C# приложения с параметрами
local command = string.format('"%s" --processing_time %d --idle_timeout %d --reconnect_interval %d', 
                              app_path, processing_time, idle_timeout, reconnect_interval)

-- Выполнение команды
os.execute(command)
