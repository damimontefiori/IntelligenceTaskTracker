# Propuesta de Arquitectura — IntelligenceTaskTracker

Estado: versión mínima viable (MVP) enfocada en simplicidad y despliegue rápido.

## 1) Objetivos y enfoque
- Mantenerse 100% en ecosistema Microsoft.
- Arquitectura MVC simple, monolítica, fácil de entender y mantener.
- SQL relacional (SQL Server) con EF Core (Code-First) y migraciones.
- Comenzar con lo mínimo indispensable y crecer por iteraciones.

## 2) Stack tecnológico
- Backend/UI: ASP.NET Core 8 MVC (Razor Views).
- ORM: Entity Framework Core 8 (Code-First, migraciones).
- Base de datos: SQL Server (LocalDB/Express para dev, Azure SQL o SQL Server para prod).
- DI/Logging: nativo de .NET (Microsoft.Extensions.*). Serilog opcional más adelante.
- Frontend: Bootstrap 5 básico para un UI minimalista.
- Pruebas: xUnit + FluentAssertions (opcional en MVP).
- Hospedaje: IIS on-prem o Azure App Service (despliegue con `dotnet publish`).

## 3) Estilo arquitectónico
Monolito MVC en una sola aplicación web. Separación lógica por carpetas (Controllers, Models, Views, Data, Services). Servicios de dominio sencillos detrás de interfaces para facilitar pruebas y evolución.

Estructura sugerida del proyecto (simple):
- Controllers: Home, Dashboard, Tasks, Users
- Models (Dominio): User, TaskItem, TaskComment, TaskStatus (enum), AuditLogEntry
- ViewModels: modelos livianos para vistas (Dashboard, Task, User)
- Services: ITaskService, IUserService, IAuditLogService + implementaciones
- Data: AppDbContext, SeedData
- Views: Razor por área (Shared, Home, Dashboard, Tasks, Users)

## 4) Modelo de dominio (MVP)
- User
  - Id (int, PK)
  - Name (nvarchar(200), requerido, único opcional)
  - CreatedAt (datetime2)
- TaskItem
  - Id (int, PK)
  - Title (nvarchar(200), requerido)
  - Description (nvarchar(max))
  - Status (enum: NotAssigned, New, InProgress, Completed)
  - CreatedAt (datetime2, requerido)
  - DueDate (datetime2, null)
  - ResponsibleUserId (int, null, FK->User)
- TaskComment (log inalterable de avances)
  - Id (int, PK)
  - TaskId (int, FK->TaskItem)
  - Comment (nvarchar(max), requerido)
  - CreatedAt (datetime2, requerido)
  - CreatedBy (nvarchar(200) simple en MVP; si luego hay auth, se enlaza a identidad)
- AuditLogEntry (mínimo, extensible)
  - Id (int, PK)
  - Entity (nvarchar(100))
  - EntityId (int)
  - Action (nvarchar(50): Created/Updated/Deleted/Commented)
  - Details (nvarchar(max))
  - CreatedAt (datetime2)

Reglas clave:
- Un usuario puede ser responsable de 0..n tareas.
- Tareas pueden no tener responsable (Status puede ser NotAssigned o la vista las agrupa como "Not Assigned").
- No se puede eliminar un usuario si tiene tareas asignadas (en DB usar ON DELETE RESTRICT/NO ACTION + validación en servicio).
- TaskComment es append-only: no se edita ni borra (sólo insert).

Índices sugeridos:
- TaskItem: IX_TaskItem_Status, IX_TaskItem_ResponsibleUserId, IX_TaskItem_DueDate
- User: IX_User_Name (único opcional para evitar duplicados)

## 5) Persistencia y migraciones
- EF Core Code-First con `AppDbContext` y `DbSet<>` para entidades.
- Migraciones: `Add-Migration InitialCreate` y `Update-Database` para dev.
- Estrategia de borrado:
  - User: RESTRICT si tiene TaskItems.
  - TaskItem: CASCADE para TaskComment (se elimina el hilo de comentarios si se elimina la tarea).
- Concurrencia: agregar `RowVersion` (rowversion/timestamp) más adelante si es necesario.

## 6) Casos de uso y controladores (MVC)
- UsersController
  - GET Index: listar usuarios
  - GET Create / POST Create: alta (nombre)
  - GET Edit / POST Edit: edición
  - POST Delete: baja; valida que no existan tareas asignadas
- TasksController
  - GET Index: listar (filtro simple por estado/usuario)
  - GET Create / POST Create: alta de tarea
  - GET Edit / POST Edit: edición (título, descripción, responsable, estado, fecha límite)
  - GET Details: ver detalle + comentarios
  - POST AddComment: agrega comentario (append-only)
  - POST ChangeStatus: cambio rápido de estado
- DashboardController
  - GET Index: vista Kanban por estado (columnas: Not Assigned, New, In Progress, Completed; orden dentro de columna: New, In Progress, Completed)
  - GET ByUser: vista por responsable (agrupa por usuario e incluye columna Not Assigned). Filtro por recurso.

Servicios (lógica de negocio básica):
- IUserService: CRUD + validación de eliminación segura.
- ITaskService: CRUD, asignación de responsable, cambio de estado, listado para dashboard.
- IAuditLogService: registrar eventos (creación, actualización, comentarios) para auditoría básica.

## 7) Vistas y UX (mínimo)
- Layout simple con Bootstrap 5.
- Dashboard Kanban: columnas con tarjetas; acciones rápidas de cambiar estado y asignar responsable.
- Vista por recurso: agrupación por usuario con columna adicional "Not Assigned".
- Formularios sencillos para CRUD de tareas/usuarios.
- Validaciones del lado del servidor (ModelState) y anotaciones de datos.

## 8) Configuración
- `appsettings.json`
  - ConnectionStrings: DefaultConnection (SQL Server LocalDB en dev)
  - Ejemplo dev: `Server=(localdb)\\MSSQLLocalDB;Database=IntelligenceTaskTracker;Trusted_Connection=True;MultipleActiveResultSets=true;`
- `appsettings.Development.json`: overrides locales.
- Inyección de dependencias en `Program.cs`:
  - `AddDbContext<AppDbContext>(UseSqlServer(connection))`
  - `AddScoped<IUserService, UserService>()`, etc.

## 9) Seguridad (posterior)
- MVP sin autenticación.
- Futuro: Microsoft Entra ID (Azure AD) o ASP.NET Core Identity si se requiere multiusuario con permisos.
- Al habilitar identidad, `TaskComment.CreatedBy` pasa a enlazarse al usuario autenticado.

## 10) Auditoría y trazabilidad
- Cada operación relevante registra un `AuditLogEntry` con detalles legibles.
- Los comentarios son el log inalterable de avances de la tarea.
- No se exponen operaciones de edición/eliminación de comentarios.

## 11) Estrategia de despliegue
- Dev: `dotnet run`, DB LocalDB/Express; migraciones aplicadas al iniciar o vía CLI.
- QA/Prod: IIS o Azure App Service + Azure SQL/SQL Server. Despliegue con `dotnet publish -c Release`.
- Migraciones: aplicadas por pipeline o al iniciar (opción controlada por config).

## 12) Pruebas (ligeras en MVP)
- Unit tests de servicios (reglas: no borrar usuario con tareas, ordenamiento en dashboard, cambio de estado, comentarios append-only).
- Tests de integración opcionales para AppDbContext en SQL local.

## 13) Roadmap incremental
- Fase 0: Esqueleto MVC, DbContext, migración inicial, seed mínimo (1-2 usuarios, 2-3 tareas).
- Fase 1: CRUD de Usuarios y Tareas con validaciones, lista en tabla.
- Fase 2: Dashboard Kanban + vista por recurso, filtros básicos.
- Fase 3: Comentarios de tareas (append-only) + auditoría básica.
- Fase 4: Mejoras UX (drag & drop), búsqueda, paginación liviana.
- Fase 5: IA de avances (resúmenes por recurso y por tarea, alertas locales + análisis IA, botón "Actualizar resumen").
- Fase 6: Autenticación/Autorización si se requiere, logging avanzado, RowVersion.

## 14) Consideraciones de calidad
- Validaciones con DataAnnotations + reglas en servicios.
- Manejo de errores simple con páginas de error y logging.
- Nombres y mensajes en español.
- Mantener bajo acoplamiento: controladores delgados, servicios con lógica.

## 15) IA de avances (resúmenes y alertas)
Objetivo
- Generar resúmenes automáticos (por recurso y por tarea) a partir de los comentarios y metadatos de tareas.
- Emitir alertas simples locales (fechas/actividad) y complementarlas con análisis de lenguaje de un proveedor de IA.

Componentes
- InsightsService (IInsightsService, AiInsightsService):
  - GetUserInsightAsync(userId, forceRefresh=false)
  - GetTaskInsightAsync(taskId, forceRefresh=false)
  - Aplica reglas locales (OVERDUE, DUE_SOON, STALE) y combina con salida IA.
- Proveedor IA intercambiable (IAiProvider):
  - Implementaciones: Gemini (por defecto), Azure OpenAI (opcional).
  - Configurable vía appsettings/variables de entorno.
- Caché (opcional, recomendado):
  - Tabla InsightsCache: Scope(User|Task), RefId, Model, ContentJson, CreatedAt, ExpiresAt.
  - TTL configurable (p. ej., 12 h). Invalidación en: nuevo comentario, cambio de estado, cambio de dueDate.

Flujo
1) El controlador solicita el insight (usuario o tarea).
2) InsightsService revisa caché; si expirado/ausente o forceRefresh, arma prompt y llama al proveedor IA.
3) Combina alertas locales + resultado IA y retorna un contrato JSON tipado (Summary, Status, RiskLevel, Alerts, NextActions).
4) La vista muestra panel con resumen, chips de alertas y botón "Actualizar resumen".

UX y puntos de integración
- Dashboard/ByResource: panel "Resumen IA del recurso" con botón "Actualizar".
- Tasks/Details: panel "Resumen IA de la tarea" con alertas y próximos pasos.

Seguridad y costos
- No enviar datos sensibles innecesarios. Truncar comentarios a últimos N por tarea y máximo M tareas por recurso.
- Timeouts cortos (8–12 s) con 1 reintento. Si falla, mostrar solo alertas locales.
- Telemetría básica: tiempo de respuesta, tasa de fallos, aciertos de caché.

Configuración
- Se agrega una sección AI en configuración. Por defecto se usará Gemini.
- Recomendado: no almacenar claves en el repositorio; usar variables de entorno o user-secrets.
```json
{
  "AI": {
    "Provider": "Gemini", // Gemini | AzureOpenAI
    "Gemini": {
      "ApiKey": ""
    },
    "AzureOpenAI": {
      "Endpoint": "",
      "Deployment": "",
      "ApiKey": ""
    },
    "Limits": {
      "MaxCommentsPerTask": 10,
      "MaxTasksPerUser": 20,
      "CacheTtlHours": 12,
      "TimeoutSeconds": 12
    }
  }
}
```

Modelo de datos
- Nueva entidad: InsightsCache (si se usa caché persistente). Índices: (Scope, RefId), ExpiresAt.

Roadmap técnico
1) Agregar IInsightsService/AiInsightsService y proveedor IA (Gemini por defecto).
2) Añadir InsightsCache (opcional) y migración.
3) Integrar en Dashboard/ByResource y Tasks/Details (panel + botón "Actualizar resumen").
4) Invalidar caché al agregar comentario o cambiar estado/dueDate.
5) Telemetría mínima y manejo de errores/timeout.

Esta propuesta prioriza la claridad y el menor número de piezas para empezar rápido, dejando puntos de extensión claros para crecer sin reescribir.
