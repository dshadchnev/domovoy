# Domovoy.Domain

## Overview
Domain entities (Device, Sensor, Actuator, Scenario, etc.) representing the core Domain-Driven Design model.

## Architecture Note
This project is pure C# with ZERO infrastructure dependencies (Npgsql, EF Core, MassTransit, etc.) following Clean Architecture principles.

Currently, microservices use flattened EF entities for performance/simplicity. As the codebase matures, microservices will map to/from these rich domain entities.