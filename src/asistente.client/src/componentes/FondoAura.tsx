/**
 * Fondo ambiental "Ember Dusk" (Aura).
 *
 * Cuatro capas de degradado con modos de fusión sobre el color base del `body`. Es
 * puramente decorativo: `aria-hidden` y sin eventos de puntero.
 *
 * Las capas NO llevan color de fondo propio. `mix-blend-mode` compone contra lo que hay
 * DETRÁS del elemento, así que el color base tiene que estar en el `body`; si se lo
 * pusiéramos al contenedor, las capas se fusionarían contra sí mismas y el efecto se
 * lavaría.
 *
 * El diseño original incluye una textura de grano sobre las capas. Se omite a propósito:
 * sobre una interfaz densa el ruido compite con el texto en lugar de aportar textura.
 *
 * Se monta fuera de la aplicación (ver `main.tsx`) y se posiciona fijo, de modo que cubre
 * el viewport sin participar del layout ni depender de qué pantalla esté visible.
 */
export function FondoAura() {
  return (
    <div className="aura" aria-hidden="true">
      <div className="aura__capa aura__capa--1" />
      <div className="aura__capa aura__capa--2" />
      <div className="aura__capa aura__capa--3" />
      <div className="aura__capa aura__capa--4" />
    </div>
  );
}
