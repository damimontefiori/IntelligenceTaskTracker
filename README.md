El requerimiento principal que dio origen a IntelligentTaskTracker fue la necesidad de un sistema sencillo y eficiente para gestionar tareas de equipos de desarrollo, con foco en:
•	Visualización Kanban y por recurso (usuario responsable)
•	Seguimiento de avances mediante comentarios inmutables
•	Alertas automáticas por IA sobre tareas próximas a vencer, atrasadas o sin actividad
•	Resúmenes inteligentes generados por modelos de lenguaje (OpenAI/Gemini)
•	Gestión de usuarios y tareas con CRUD completo
La solución debía ser fácil de desplegar, extensible y con una interfaz amigable, permitiendo a los equipos enfocarse en la entrega y el seguimiento de objetivos.
Arquitectura:
La aplicación IntelligentTaskTracker está diseñada siguiendo el patrón MVC (Model-View-Controller), una arquitectura clásica. 
El patrón MVC (Model-View-Controller) organiza la aplicación en tres componentes principales: 
1.	El Modelo gestiona los datos y la lógica de negocio (por ejemplo, la clase User)
2.	La Vista presenta la información al usuario y recibe sus interacciones.
3.	El Controlador actúa como intermediario, procesando las solicitudes del usuario, actualizando el modelo y seleccionando la vista adecuada. 
Esta separación facilita el mantenimiento, la escalabilidad y la colaboración en el desarrollo de aplicaciones web.
