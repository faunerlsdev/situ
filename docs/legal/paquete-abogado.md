# Paquete para el abogado

Fecha: 2026-10-27. Este archivo recoge lo que el producto ya decidió y lo que sigue abierto para un dictamen de Habeas Data (Ley 1581 de 2012). No es un concepto jurídico.

## Pregunta abierta: teléfono del menor (RF-MIN-06)

El registro pide el teléfono en formato internacional E.164. Sirve para la seguridad de la cuenta y, más adelante, para verificación en dos pasos. No se muestra a otros usuarios, no se exige que sea único y la API no dice si un número ya está registrado.

La clave `registration.phone_required_for_minors` nace en `true`: un menor debe dejar teléfono. Si el panel la pone en `false`, el teléfono queda opcional solo para menores. Los adultos siempre lo dejan.

Pregunta: ¿pedir el teléfono a un menor cumple la minimización, o debe nacer opcional para esa edad?

## Otras preguntas ya registradas

- Si la firma digital interna del correo de constancia basta como prueba de la autorización del representante (Ley 527 de 1999).
- Si el flujo de menores y las transmisiones anónimas de menores quedan cubiertos por el interés superior del menor.
- Si los proyectos de ley de edad obligan a subir el mínimo de 14 años o a encender la verificación de identidad (RF-MIN-12).
