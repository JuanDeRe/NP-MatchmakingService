# Nightwatch Protocol - MatchmakingService

https://github.com/JuanDeRe/NP-MatchmakingService/actions/workflows/ci.yml/badge.svg](https://github.com/JuanDeRe/NP-MatchmakingService/actions/workflows/ci.yml)

`MatchmakingService` es el servicio encargado de administrar la cola de jugadores y crear partidas para Nightwatch Protocol.

Este servicio recibe solicitudes de jugadores que quieren entrar a una partida, los mantiene en una cola temporal y, cuando hay suficientes jugadores disponibles, solicita al `GameService` la creación de una nueva partida. Luego guarda la asignación de cada jugador con su `matchId`, `role` y `gameServiceId`.

## Responsabilidades principales

- Recibir jugadores que entran a matchmaking.
- Evitar que un jugador entre dos veces a la cola.
- Agrupar jugadores cuando haya suficientes para crear una partida.
- Asignar roles iniciales: `Guard` y `Animatronic`.
- Solicitar al `GameService` la creación de partidas.
- Guardar temporalmente la asignación de jugadores a partidas.
- Permitir consultar el estado de matchmaking de un jugador.
- Permitir consultar la cola actual para depuración.

## Tecnologías

- C# / .NET
- ASP.NET Core Web API
- HTTP/REST
- xUnit para pruebas unitarias
- Comunicación interna con `GameService` mediante HTTP

## Estructura del proyecto

MatchmakingService/
├── Clients/
│   ├── IGameServiceClient.cs
│   └── GameServiceClient.cs
├── Configuration/
│   ├── GameServiceOptions.cs
│   └── MatchmakingOptions.cs
├── Controllers/
│   └── MatchmakingController.cs
├── Contracts/
├── Models/
├── Services/
├── Stores/
├── test/
│   └── MatchmakingService.Tests/
├── Program.cs
├── appsettings.json
├── MatchmakingService.csproj
├── MatchmakingService.sln
└── README.md

## Configuración

En `appsettings.json` se define la URL del `GameService` y la configuración mínima de matchmaking.

Ejemplo:

{
"GameService": {
"BaseUrl": "http://localhost:5002"
},
"Matchmaking": {
"MinPlayersPerMatch": 2,
"MaxPlayersPerMatch": 5
},
"AllowedHosts": "*"
}

Para desarrollo local, el servicio corre por defecto en:

http://localhost:5004

## Ejecutar el servicio

Primero debe estar corriendo el `GameService` en:

http://localhost:5002

Luego, desde la raíz del proyecto `MatchmakingService`, ejecutar:

dotnet restore
dotnet run

## Endpoints disponibles

### Entrar a cola

POST http://localhost:5004/matchmaking/join

Body:

{
"playerId": "player-001"
}

Respuesta cuando todavía no hay suficientes jugadores:

{
"status": "Queued",
"message": "Player added to queue",
"matchId": null,
"gameServiceId": null,
"role": null
}

Respuesta cuando se crea una partida:

{
"status": "Assigned",
"message": "Player assigned to match",
"matchId": "match-id",
"gameServiceId": "game-service-local",
"role": "Animatronic"
}

### Consultar estado de un jugador

GET http://localhost:5004/matchmaking/status/{playerId}

Ejemplo:

GET http://localhost:5004/matchmaking/status/player-001

Respuesta si el jugador está en cola:

{
"playerId": "player-001",
"status": "Queued",
"matchId": null,
"gameServiceId": null,
"role": null
}

Respuesta si el jugador ya fue asignado:

{
"playerId": "player-001",
"status": "Assigned",
"matchId": "match-id",
"gameServiceId": "game-service-local",
"role": "Guard"
}

### Consultar cola actual

GET http://localhost:5004/matchmaking/queue

Respuesta ejemplo:

[
{
"playerId": "player-001",
"joinedAtUtc": "2026-07-01T20:30:00Z"
}
]

## Flujo actual

1. Un jugador entra a la cola mediante `/matchmaking/join`.
2. El servicio valida que el jugador no esté ya en cola o asignado.
3. Si no hay suficientes jugadores, el jugador queda en estado `Queued`.
4. Cuando hay suficientes jugadores, el servicio crea un grupo.
5. El primer jugador del grupo recibe el rol `Guard`.
6. Los demás jugadores reciben el rol `Animatronic`.
7. El servicio llama al `GameService` para crear la partida.
8. El `GameService` responde con un `matchId`.
9. El `MatchmakingService` guarda la asignación de cada jugador.
10. Cada jugador puede consultar su estado en `/matchmaking/status/{playerId}`.

## Pruebas unitarias

El proyecto incluye pruebas unitarias para validar la lógica principal del matchmaking.

Para ejecutar las pruebas:

dotnet test

Pruebas cubiertas actualmente:

- Agregar jugador a cola cuando no hay suficientes jugadores.
- Crear partida cuando hay suficientes jugadores.
- Consultar asignación del primer jugador.
- Evitar que un jugador entre dos veces a la cola.
- Devolver asignación si el jugador ya estaba asignado.
- Retornar jugadores a cola si el `GameService` no está disponible.

## Notas arquitectónicas

El `MatchmakingService` es un servicio separado del `GameService`.

El `MatchmakingService` no administra el estado interno de una partida. Su responsabilidad es formar grupos de jugadores y solicitar al `GameService` la creación de partidas.

El estado de cola y asignaciones se mantiene temporalmente en memoria para esta primera versión. Más adelante, esta información puede moverse a Redis para soportar múltiples instancias del servicio.

## Próximos pasos

- Integrar autenticación con JWT.
- Obtener `playerId` desde el token en vez de recibirlo en el body.
- Reemplazar almacenamiento en memoria por Redis para soportar múltiples instancias.
- Consultar capacidad real de instancias de `GameService`.
- Registrar métricas operativas de matchmaking.
- Integrar el flujo completo con `RealtimeGateway`.