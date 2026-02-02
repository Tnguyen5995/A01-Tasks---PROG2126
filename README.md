# A01-Tasks - PROG2126

How to run

Server (on Computer A)

Example (Task mode):
ServerApp.exe --port 5000 --maxMb 5 --log ServerLog.txt --mode task

# for longer run
# server
dotnet run --project PROG2126_A01_TaskTcpPerf/ServerApp -- --maxMb 500 --log ServerLog.txt --mode task

# client
dotnet run --project PROG2126_A01_TaskTcpPerf/ClientApp -- --workers 1 --payloadBytes 65536 --delayMs 5 --mode task



Thread mode:
ServerApp.exe --port 5000 --maxMb 5 --log ServerLog.txt --mode thread


Client (on Computer B)

Example (10 “client threads” / workers, 512 bytes payload, Task mode):
ClientApp.exe --server 192.168.1.50 --port 5000 --workers 10 --payloadBytes 512 --delayMs 5 --mode task

Thread mode:
ClientApp.exe --server 192.168.1.50 --port 5000 --workers 10 --payloadBytes 512 --delayMs 5 --mode thread


Core requirements mapping:

R1 Multi-computer support: Server binds to IPAddress.Any, client connects via LAN IP and port.

R2 Server writes all client comms into ONE file: Single shared log file with timestamp + endpoint + message.

R3 File-size shutdown + notify clients + graceful stop: When file reaches max bytes → server replies STOP → clients stop sending → server cancels workers → waits for them → closes file.

R4 Performance experiments + time metric: Client measures round-trip time (Stopwatch ticks → ms). Server measures file write timing + throughput (msgs/sec, bytes/sec).

R5 Team-defined extra requirements: (add your team’s) e.g. message format, payload size, number of workers, delay, error handling rules, port rule, log line format.