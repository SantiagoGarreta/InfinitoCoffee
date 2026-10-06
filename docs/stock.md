# Control de stock

La sección **Administración → Stock** muestra el saldo compartido de cada ingrediente y el saldo de productos terminados de la sucursal seleccionada. Solo los administradores pueden registrar artículos, recetas y ajustes. El catálogo de artículos y las recetas son comunes a ambas sucursales.

## Puesta en marcha

Aplicar las migraciones con `dotnet run --project .\src\InfinitoCoffee.DbSetup -- migrate`. La migración `20261004232238_AddBranches` crea Sucursal 1 y Sucursal 2, conserva los ingredientes en un saldo compartido y asigna los productos terminados existentes a Sucursal 1. Sucursal 2 comienza con cero productos terminados. Los movimientos anteriores permanecen en el historial. Ver la [guía de sucursales](sucursales.md).

Si ya se usaba el registro de producción, los productos terminados existentes conservarán su saldo y sus ingredientes ya descontados. Si se usó la versión que descontaba ingredientes también al entregar pedidos, revisar esos movimientos del historial y corregir sus saldos con un ajuste documentado.

1. Crear los ingredientes con su unidad (unidades, gramos o mililitros).
2. Crear los productos terminados y vincularlos con los productos del catálogo.
3. Guardar una receta para cada producto que consuma ingredientes. La receta indica cuántas unidades rinde y la cantidad de cada ingrediente para ese rendimiento.
4. Usar **Ajustar stock → Aumentar** para cargar las cantidades iniciales de ingredientes. Luego seleccionar la sucursal e ingresar los productos terminados: **Producción** consume ingredientes; **Ingreso sin producción** permite cargar existencias ya producidas o recibidas del otro local. El motivo es obligatorio.

## Uso diario

- **Aumentar un producto terminado como producción:** se suman sus unidades a la sucursal seleccionada y se descuentan los ingredientes compartidos de la última receta, proporcionalmente a la cantidad ingresada. Ambos cambios se guardan juntos. Si faltan ingredientes, no se registra el producto. Un producto sin receta puede ingresarse, pero no consume ingredientes.
- **Ingresar un producto sin producción:** se suman unidades únicamente al local seleccionado. No consume ingredientes. Usar esta opción para traslados o correcciones de productos ya preparados.
- **Ajustar stock:** aumentar ingredientes por compras o disminuir artículos por pérdidas, descartes y correcciones, siempre con un motivo. Disminuir un producto terminado no modifica ingredientes.
- **Vender un producto:** se descuentan solo los productos terminados de la sucursal del pedido, en caja para artículos de Cantina o al entregar para los demás. Si faltan productos en ese local, no se confirma la venta o entrega, aunque existan en el otro. Los ingredientes ya se descontaron en la producción.
- **Actualizar una receta:** crea una versión nueva. Los próximos ingresos de productos terminados usan la última versión; los movimientos anteriores conservan las cantidades descontadas.
- **Consultar historial:** muestra movimientos de productos del local seleccionado y movimientos de ingredientes compartidos de ambas sucursales. Cada operación identifica la sucursal que la originó, el motivo, usuario, saldo anterior, cambio y saldo resultante. Las ventas muestran el pedido asociado.

Por ejemplo, con 30 huevos y una receta de 10 scones que usa 5 huevos, producir 10 en Sucursal 1 y 10 en Sucursal 2 deja 20 huevos compartidos y 10 scones en cada local. Vender 3 en Sucursal 1 deja 20 huevos, 7 scones allí y 10 en Sucursal 2. El consumo proporcional al ingreso se redondea a tres decimales.

Para trasladar 4 scones de Sucursal 1 a Sucursal 2: disminuir 4 en Sucursal 1, cambiar a Sucursal 2 y aumentar 4 eligiendo **Ingreso sin producción**. Registrar el mismo motivo en ambos ajustes. Son dos registros manuales; si se completa solo uno, hay que completar o corregir el otro. El traslado no crea ventas ni vuelve a consumir ingredientes.

Los productos del catálogo sin artículo de stock vinculado siguen pudiéndose entregar sin descuentos. Los pedidos ya entregados no se recalculan al crear o cambiar una receta.
