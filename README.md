# client-rabbit-server with ttl and lua

# # Lua Server Configuration and Execution

# # Overview

This project provides a Lua script to configure and run a .NET server application. The configuration is managed via a JSON file and executed with Lua scripting for dynamic parameter handling.

```sh
chcp 65001
lua run_client.lua 5002
lua send_message.lua 5002 "{\"id\":1,\"message\":\"Hello, server!\",\"timestamp\":\"2024-12-31T12:00:00Z\"}"
lua run_server.lua
```

# #Requirements

Lua interpreter

Lua JSON library (e.g., Lua CJSON)

.NET SDK (to run the server DLL)

A valid config.json file in the script directory

# # File Structure

project-directory/
├── server_config.json       # Configuration file with server parameters
├── run_server.lua    # Lua script to read config and run server
├── server/           # Directory containing .NET server application
│   └── bin/Debug/net8.0/server.dll  # Compiled server application

Configuration File

# #  The config.json file should include the following parameters:

```sh
{
  "Port": 5002,
  "ReconnectTimer": 5000,
  "IdleTimer": 15000,
  "ProcessingDelay": 10000
}
```

Parameter Descriptions:

- Port: Port number on which the server will listen.

- ReconnectTimer: Time in milliseconds for reconnection attempts.

- IdleTimer: Time in milliseconds for idle timeout.

- ProcessingDelay: Time in milliseconds to simulate processing delay.

## Usage

Setting Up Environment

- Ensure the Lua interpreter is installed and accessible from the command line.

- Install the Lua JSON library:

```sh
luarocks install dkjson
luarocks install luafilesystem
luarocks install luasocket
luarocks install luazip
luarocks install md5
```

- Verify the .NET server application is built and available at server/bin/Debug/net8.0/server.dll.

Running the Script

- Open the terminal in the project directory.

- Execute the Lua script:

```sh
lua run_server.lua
```

Expected Output

The script reads the config.json file, forms a command, and executes the .NET server application. Example:

```sh
Запуск приложения на порту: 5002
Выполняется команда: dotnet "../server/bin/Debug/net8.0/server.dll" --port=5002 --reconnect-timer=5000 --idle-timer=15000 --processing-delay=10000
Приложение успешно запущено на порту 5002
```

Error Handling

If the configuration file is missing or invalid, the script terminates with an error.

If the server application fails to execute, the script logs the error.

Customization

To modify the configuration, edit the config.json file and rerun the script. For advanced configurations, extend the Lua script to handle additional parameters or dynamic inputs.

Troubleshooting

Ensure all required libraries are installed.

Verify the .NET server application is compiled and located at the expected path.

Check config.json for syntax errors or missing parameters.

License

MIT

Free Software, Hell Yeah!