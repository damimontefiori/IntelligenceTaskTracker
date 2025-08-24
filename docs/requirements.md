# Requerimientos Funcionales - Aplicación de Asignación de Tareas

## 1. Gestión de Usuarios
- Alta, edición y baja de miembros del equipo (solo nombre).
- Los usuarios pueden ser responsables de tareas.
- Solo se pueden eliminar usuarios si no tienen tareas asignadas.

## 2. Gestión de Tareas
- Crear, editar y eliminar tareas.
- Cada tarea debe tener: título, descripción, responsable, estado, fecha de creación, fecha límite (opcional).
- Estados posibles de la tarea: `New`, `In Progress`, `Completed`.
- Las tareas pueden asignarse solo a un usuario.
- El usuario responsable debe ser seleccionado de una lista al asignar la tarea.
- Las tareas pueden estar sin asignar (columna "Not Assigned").

## 3. Seguimiento y Actualización de Avances
- Los responsables pueden actualizar el estado y el avance de la tarea.
- Se debe poder agregar comentarios o notas de avance en cada tarea.
- Cada tarea mantiene un log inalterable de los detalles y comentarios agregados.
- En la vista Detalles, el campo "Tu nombre" se autocompleta con el nombre del responsable de la tarea (si existe).
- Los comentarios se muestran ordenados de más reciente a más antiguo.
- En la vista Edición de tarea, se muestran los comentarios en modo solo lectura (no editables ni eliminables).
- Al navegar al detalle de una tarea desde otra vista (lista o dashboard), el botón "Volver" debe regresar a la vista anterior conservando filtros y parámetros (se implementa pasando un returnUrl local y usando fallback al índice si no existe).

## 4. Visualización tipo Dashboard
- Vista principal tipo Kanban, mostrando las tareas por estado en columnas (`Not Assigned`, `New`, `In Progress`, `Completed`).
- Vista alternativa por recurso, mostrando las tareas agrupadas por responsable, incluyendo una columna "Not Assigned" para tareas sin responsable.
- Dentro de cada columna (en ambas vistas), las tareas deben ordenarse dejando arriba las `New`, en el medio las `In Progress` y al final las `Completed`.
- Se puede filtrar por recurso en la vista por recurso.
- Visualización minimalista y clara, enfocada en la gestión rápida durante sesiones de revisión.

## 5. Sesiones de Revisión
- Permitir filtrar y visualizar tareas por responsable durante reuniones.

## 6. Simplicidad y Minimalismo
- Interfaz sencilla, sin funcionalidades complejas ni configuraciones avanzadas.
- Inspirada en aplicaciones como Microsoft Planner y Trello, pero con menor complejidad.

## 7. Inteligencia y Resúmenes (IA)
- La vista "Por Recurso" mostrará un resumen automático del avance del recurso, generado a partir del análisis de los comentarios de las tareas asignadas:
  - Resumen general del progreso, riesgos y próximos pasos.
  - Alertas simples basadas en fechas y actividad (p. ej., DUE_SOON, OVERDUE, STALE) aplicadas por reglas locales.
- La vista "Detalles de Tarea" mostrará un resumen por tarea con:
  - Estado estimado (OnTrack, AtRisk, OffTrack), riesgos y próximos pasos.
  - Alertas simples por fechas/actividad.
- El proveedor de IA será configurable. Por defecto: Gemini.
- El análisis considerará un subconjunto acotado de datos para eficiencia (p. ej., últimos N comentarios por tarea y hasta M tareas por recurso).
- Debe existir un botón "Actualizar resumen" para regenerar el análisis bajo demanda.
- En caso de error o timeout del proveedor de IA, se mostrarán solo las alertas locales y un mensaje "Resumen IA no disponible".
- El resumen es informativo; no modifica datos de negocio ni permite edición desde estos paneles.

**Notas:**  
- No se requiere integración con otras plataformas.
- El sistema debe ser fácil de usar y mantener.