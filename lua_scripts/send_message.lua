local http = require("socket.http")
local ltn12 = require("ltn12")
local json = require("dkjson") -- Используем dkjson для работы с JSON

-- Функция для отправки сообщения
local function send_message(port, json_message)
    local url = "http://localhost:" .. tostring(port) .. "/send-request"
    local response_body = {}

    -- Проверяем валидность JSON
    local request_body, encode_error = json.encode(json_message)
    if not request_body then
        print("Ошибка при кодировании JSON:", encode_error)
        os.exit(1)
    end

    -- Формирование заголовков
    local headers = {
        ["Content-Type"] = "application/json",
        ["Content-Length"] = tostring(#request_body)
    }

    -- Отправка POST-запроса
    local res, code, response_headers, status = http.request{
        url = url,
        method = "POST",
        headers = headers,
        source = ltn12.source.string(request_body),
        sink = ltn12.sink.table(response_body)
    }

    -- Проверка результата
    if not res then
        print("Ошибка при отправке запроса:", code)
    else
        print("Ответ от сервера:", table.concat(response_body))
        print("HTTP-код ответа:", code)
    end
end

-- Получение порта и сообщения из аргументов командной строки
local port = arg[1]
local raw_message = arg[2]

if not port or not raw_message then
    print("Пожалуйста, укажите порт и сообщение в формате JSON!")
    print("Пример: lua send_message.lua 5001 '{\"key\":\"value\"}'")
    os.exit(1)
end

-- Попытка декодирования JSON
local decoded_message, pos, decode_error = json.decode(raw_message)
if not decoded_message then
    print("Ошибка при разборе JSON:", decode_error)
    os.exit(1)
end

-- Отправка сообщения
send_message(port, decoded_message)
