# RSOI Lab 3 - Fault Tolerance 
 
Dockerized microservices with Circuit Breaker. 
## Services 
- FlightService (port 8081) 
- TicketService (port 8082) 
- AirlineService (port 8083) 
- GatewayService (port 8080) 
 
## Run 
docker compose up -d --build 
 
## Test endpoints 
- GET http://localhost:8081/manage/health 
- GET http://localhost:8080/api/flights 
- POST http://localhost:8080/api/book 
