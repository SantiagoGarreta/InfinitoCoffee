# Control de stock

La sección **Administración → Stock** (`/admin/stock`) está disponible únicamente para administradores. La API aplica el mismo permiso a todas las consultas y operaciones del módulo. Caja y cocina mantienen su trabajo habitual; la entrega de un pedido descuenta automáticamente los productos que tienen control de stock.

## Activación

Aplicar la migración `20260924231017_AddStockControl` con el comando habitual de `InfinitoCoffee.DbSetup migrate` antes de arrancar esta versión de la API. En producción, el workflow existente ejecuta las migraciones antes de actualizar los contenedores. La migración agrega tablas; no cambia pedidos ni existencias anteriores.

1. En **Artículos**, crear los ingredientes con su unidad: unidades, gramos o mililitros.
2. Agregar los productos terminados, vinculándolos a productos existentes del catálogo.
3. Registrar las existencias iniciales reales en **Registrar movimiento → Ingreso / compra**, indicando fábrica o cafetería y la referencia “Stock inicial”. Se pueden cargar varios artículos juntos.
4. Crear las recetas por lote, por ejemplo: 10 scones requieren 5 huevos y las cantidades correspondientes de los otros ingredientes.

Al vincular un producto, sus existencias comienzan en cero. Cargar primero el stock inicial de cafetería antes de entregar pedidos de ese producto. Los productos sin vinculación siguen funcionando como antes. No se descuentan retroactivamente pedidos ya entregados.

## Operación diaria

- **Ingreso / compra:** registrar lo efectivamente recibido. Para ingredientes medidos en gramos se puede ingresar kg; para mililitros se pueden ingresar litros. El sistema convierte todo a la unidad base. La referencia permite anotar proveedor, factura o remito sin crear otro formulario.
- **Producción:** elegir una receta, ubicación y cantidad de unidades de la tanda. La pantalla calcula y muestra los ingredientes necesarios. Confirmar consume los ingredientes y genera los productos terminados en una única operación.
- **Descarte de producción:** si una tanda de 20 scones dejó 2 piezas inutilizables, ingresar 20 unidades de tanda y 2 descartadas, con motivo. Se consumen los ingredientes para 20 y quedan 18 piezas utilizables. Si se ingresa solo 18 como tanda, el consumo teórico también sería para 18.
- **Envío:** elegir origen, destino y cantidades. Se descuentan del origen y aparecen en tránsito. Se admiten ingredientes y productos, en ambos sentidos.
- **Recepción:** confirmar el envío cuando llega. Las cantidades se completan con lo enviado; solo hay que corregir los faltantes. Una diferencia requiere motivo. Confirmar cierra el envío completo: lo recibido entra al destino y el faltante queda documentado en el envío y el historial. No se admiten recepciones parciales abiertas; usar envíos separados si se transportan tandas en distintos momentos. Las cantidades adicionales requieren su propio ingreso.
- **Ventas:** cuando una comanda pasa a Entregado, se descuentan sus productos vinculados de la cafetería. La entrega y el descuento se guardan juntos; si falta stock, la entrega no se confirma y se informa el conflicto. Crear o cancelar pedidos no altera existencias. Si una cancelación deja comida desperdiciada, registrar la merma correspondiente.
- **Merma:** descontar roturas, vencimientos o pérdidas con motivo obligatorio.
- **Conteo físico:** ingresar lo contado en cada artículo y explicar el ajuste. Se muestran el saldo esperado y la diferencia antes de confirmar. El historial conserva el saldo anterior, la diferencia y el nuevo saldo. Si hubo movimientos posteriores a la lectura usada para el conteo, se rechaza la confirmación para evitar sobrescribirlos. Actualizar la pantalla y repetir el conteo en ese caso.

## Ejemplo de comprobación

| Paso | Huevos en fábrica | Scones en fábrica | Scones en tránsito | Scones en cafetería |
|---|---:|---:|---:|---:|
| Recibir 20 huevos | 20 | 0 | 0 | 0 |
| Producir 20 scones (receta: 5 huevos cada 10) | 10 | 20 | 0 | 0 |
| Enviar 20 scones | 10 | 0 | 20 | 0 |
| Confirmar recepción completa | 10 | 0 | 0 | 20 |
| Entregar un pedido de 3 scones | 10 | 0 | 0 | 17 |

## Reglas de control

- Ninguna operación puede dejar stock negativo. Las operaciones con varios artículos se guardan completas o no se guarda ninguna parte.
- Unidades se cuentan como enteros; gramos y mililitros admiten tres decimales. Si una receta daría medio huevo, ingresar un lote compatible o registrar ese ingrediente por peso.
- Cada modificación de receta crea una versión nueva; las producciones conservan la versión que consumieron. Las recetas usan ingredientes, sin subrecetas anidadas.
- Todos los movimientos incluyen fecha, motivo/referencia y usuario. Las ventas automáticas identifican el pedido y aparecen como “Venta automática”. No hay edición ni eliminación de movimientos por la API; las correcciones se documentan con nuevos ingresos, mermas o conteos.
- Los reintentos de la misma operación con el mismo identificador no duplican movimientos. La pantalla conserva ese identificador después de un error mientras se mantiene la sesión de la aplicación. Tras recargar por completo el navegador y ante una respuesta incierta, revisar el historial antes de volver a cargar una operación.
- Los saldos tienen control de concurrencia. Un conteo conserva la revisión que se leyó al elegir el artículo; no puede reemplazar una venta o producción posterior.
- El stock mínimo avisa sobre ingredientes en fábrica y productos terminados en cafetería. No crea compras automáticamente.
- El historial está paginado y se puede filtrar por artículo y ubicación. Se muestran todos los envíos pendientes y los últimos 100 envíos cerrados; el historial completo permanece disponible.

Las existencias son teóricas: requieren ingresos, producción y conteos físicos confiables para detectar pérdidas. Una diferencia no demuestra por sí sola una sustracción. El módulo no infiere automáticamente la producción desde una recepción, no calcula costos de recetas y no descuenta ingredientes directamente por las bebidas vendidas: las recetas se consumen al registrar producción, también disponible en la ubicación Cafetería.

## Verificación

Las pruebas de integración cubren el circuito de huevos y scones, unidades de medida, versiones de receta, descarte de producción, falta de ingredientes, atomicidad de entrega/stock, permisos, reintentos, mermas, faltantes de envío y conteos desactualizados. Las pruebas del frontend cubren los formularios, la vista previa, conservación de datos ante errores y reintentos seguros.
