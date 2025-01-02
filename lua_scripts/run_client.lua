-- Путь к приложению client
local client_path = "D:\\PROTEI\\projects\\current task - ttl and rabbit wit slaves\\client\\client\\bin\\Debug\\net8.0\\client.dll"

-- Функция для запуска приложения client
local function run_client(client_port)
    -- Указание адреса для client
    local address = "http://localhost:" .. tostring(client_port)

    -- Формирование команды
    local command = string.format('dotnet "%s" --port=%s', client_path, client_port)

    -- Логирование команды
    print("Запуск client на адресе:", address)
    print("Выполняется команда:", command)

    -- Запуск команды
    local result = os.execute(command)

    -- Проверка результата
    if result == 0 then
        print("Приложение client успешно запущено на", address)
    else
        print("Ошибка при запуске приложения")
    end
end

-- Получение порта из аргументов командной строки
local client_port = arg[1]

if not client_port then
    print("Пожалуйста, укажите порт для client!")
    print("Пример: lua lua-client.lua 5001")
    os.exit(1)
end

-- Запуск приложения с указанным портом
run_client(client_port)
