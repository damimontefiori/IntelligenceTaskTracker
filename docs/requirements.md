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


**Notas:**  
- No se requiere integración con otras plataformas.
- El sistema debe ser fácil de usar y mantener.