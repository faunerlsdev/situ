# Paquete para el abogado

Fecha: 2026-10-27. Este archivo recoge lo que el producto ya decidió y lo que sigue abierto para un dictamen de Habeas Data (Ley 1581 de 2012). No es un concepto jurídico.

## Pregunta abierta: teléfono del menor (RF-MIN-06)

El registro pide el teléfono en formato internacional E.164. Sirve para la seguridad de la cuenta y, más adelante, para verificación en dos pasos. No se muestra a otros usuarios, no se exige que sea único y la API no dice si un número ya está registrado.

La clave `registration.phone_required_for_minors` nace en `true`: un menor debe dejar teléfono. Si el panel la pone en `false`, el teléfono queda opcional solo para menores. Los adultos siempre lo dejan.

Pregunta: ¿pedir el teléfono a un menor cumple la minimización, o debe nacer opcional para esa edad?

## Pregunta abierta: amistad entre adulto y menor

La clave `friends.allow_adult_minor` nace en `false`. Con ese valor, un adulto y un menor no pueden ser amigos ni enviarse solicitudes. Los menores solo se relacionan con menores. Un menor solo se encuentra si alguien escribe su nombre de usuario exacto y no sale en listados ni en sugerencias. La invitación por correo no se envía cuando el destinatario resulta ser menor, y la API responde igual que si el correo no tuviera cuenta.

Decisión de producto, pendiente de revisión legal: ¿la separación adulto/menor debe seguir siendo la regla, o hay un caso (por ejemplo un representante) en el que un adulto pueda ser amigo de un menor sin abrir el perfil público?

## Pregunta abierta: invitación al correo de un tercero

`POST /friends/invitations` pide un correo y envía un mensaje en español. Si ese correo no tiene cuenta, el mensaje lleva un enlace de un solo uso a la web. Si ya tiene cuenta y es un adulto con el que sí se puede ser amigo, el mensaje pide revisar las solicitudes y no incluye un enlace. Si el destinatario es un menor, no se envía nada. La respuesta de la API es la misma en los tres casos y la auditoría no guarda el correo.

Pregunta: ¿enviar esa invitación al correo de alguien que no nos lo dio cumple la Ley 1581, o hay que limitar el envío a correos que la persona ya registró?

## Otras preguntas ya registradas

- Si la firma digital interna del correo de constancia basta como prueba de la autorización del representante (Ley 527 de 1999).
- Si el flujo de menores y las transmisiones anónimas de menores quedan cubiertos por el interés superior del menor.
- Si los proyectos de ley de edad obligan a subir el mínimo de 14 años o a encender la verificación de identidad (RF-MIN-12).
