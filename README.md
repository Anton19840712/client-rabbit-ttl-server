# Client-rabbit-server with ttl and lua app

# Lua Server Configuration and Execution

# Overview

This project provides a Lua script to configure and run a .NET server application. The configuration is managed via a JSON file and executed with Lua scripting for dynamic parameter handling.

# Requirements

Lua interpreter

Lua JSON library (e.g., Lua CJSON)

.NET SDK (to run the server DLL)

A valid config.json file in the script directory

Configuration File

#  The config.json file should include the following parameters:

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

# Usage

Setting Up Environment

- Install the Lua libraries:

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

- Execute the Lua scripts in each cmd window:

```sh
chcp 65001
```

- Execute the Lua scripts in each cmd window per command:

```sh
lua run_client.lua 5002
lua send_message.lua 5002 "{\"id\":1,\"message\":\"Hello, server!\",\"timestamp\":\"2024-12-31T12:00:00Z\"}"
lua run_server.lua
```