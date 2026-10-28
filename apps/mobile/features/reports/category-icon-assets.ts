import type { ImageSourcePropType } from 'react-native';

const button = {
  accidente: require('../../assets/images/categories/button/accidente.png'),
  trafico: require('../../assets/images/categories/button/trafico.png'),
  vehiculo_varado: require('../../assets/images/categories/button/vehiculo_varado.png'),
  hurto: require('../../assets/images/categories/button/hurto.png'),
  puesto_control: require('../../assets/images/categories/button/puesto_control.png'),
  basuras: require('../../assets/images/categories/button/basuras.png'),
  hueco: require('../../assets/images/categories/button/hueco.png'),
  derrumbe: require('../../assets/images/categories/button/derrumbe.png'),
  inundacion: require('../../assets/images/categories/button/inundacion.png'),
  incendio: require('../../assets/images/categories/button/incendio.png'),
  protesta: require('../../assets/images/categories/button/protesta.png'),
  obra_via: require('../../assets/images/categories/button/obra_via.png'),
  corte_luz: require('../../assets/images/categories/button/corte_luz.png'),
  corte_agua: require('../../assets/images/categories/button/corte_agua.png'),
  peligro: require('../../assets/images/categories/button/peligro.png'),
  policia: require('../../assets/images/categories/button/policia.png'),
  emergencia: require('../../assets/images/categories/button/emergencia.png'),
  animal: require('../../assets/images/categories/button/animal.png'),
  evento: require('../../assets/images/categories/button/evento.png'),
  ruido: require('../../assets/images/categories/button/ruido.png'),
  semaforo: require('../../assets/images/categories/button/semaforo.png'),
  via_cerrada: require('../../assets/images/categories/button/via_cerrada.png'),
  otro: require('../../assets/images/categories/button/otro.png'),
} as const;

const map = {
  accidente: require('../../assets/images/categories/map/accidente.png'),
  trafico: require('../../assets/images/categories/map/trafico.png'),
  vehiculo_varado: require('../../assets/images/categories/map/vehiculo_varado.png'),
  hurto: require('../../assets/images/categories/map/hurto.png'),
  puesto_control: require('../../assets/images/categories/map/puesto_control.png'),
  basuras: require('../../assets/images/categories/map/basuras.png'),
  hueco: require('../../assets/images/categories/map/hueco.png'),
  derrumbe: require('../../assets/images/categories/map/derrumbe.png'),
  inundacion: require('../../assets/images/categories/map/inundacion.png'),
  incendio: require('../../assets/images/categories/map/incendio.png'),
  protesta: require('../../assets/images/categories/map/protesta.png'),
  obra_via: require('../../assets/images/categories/map/obra_via.png'),
  corte_luz: require('../../assets/images/categories/map/corte_luz.png'),
  corte_agua: require('../../assets/images/categories/map/corte_agua.png'),
  peligro: require('../../assets/images/categories/map/peligro.png'),
  policia: require('../../assets/images/categories/map/policia.png'),
  emergencia: require('../../assets/images/categories/map/emergencia.png'),
  animal: require('../../assets/images/categories/map/animal.png'),
  evento: require('../../assets/images/categories/map/evento.png'),
  ruido: require('../../assets/images/categories/map/ruido.png'),
  semaforo: require('../../assets/images/categories/map/semaforo.png'),
  via_cerrada: require('../../assets/images/categories/map/via_cerrada.png'),
  otro: require('../../assets/images/categories/map/otro.png'),
} as const;

const sets = { button, map } as const;

export function localCategoryIcon(slug: string | null, kind: 'button' | 'map'): ImageSourcePropType | null {
  if (!slug || !(slug in sets[kind])) {
    return null;
  }
  return sets[kind][slug as keyof typeof button];
}
