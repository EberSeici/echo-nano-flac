# Contribuir

Las contribuciones al código propio se ofrecen bajo la licencia MIT del proyecto.
Describe el problema y la solución en cada pull request. No incluyas canciones,
preferencias personales ni binarios de terceros.

Al cambiar la conversión, comprueba:

- FLAC de 16 y 24 bits: mismo audio PCM, frecuencia, bits y canales.
- Opus mono y estéreo: salida de 48 kHz/24 bits igual a la decodificación de referencia.
- Etiquetas Unicode, archivos sin etiquetas y nombres repetidos.
- Entradas dañadas: no entregar una salida parcial como si estuviera verificada.
- Cancelación entre archivos y originales intactos.

Al cambiar la interfaz, revisa tema claro/oscuro/sistema, navegación por teclado,
menús y tamaño mínimo de ventana. La aplicación debe funcionar con fondo sólido
cuando Windows no admite Mica/Acrílico.

Para errores de formato, comparte un archivo de prueba generado o que tengas
derecho a distribuir, junto con su configuración; evita datos personales.
