<template>
  <Dialog v-model:visible="visible"
          modal
          header="Wybierz lokalizację"
          :style="{ width: '95vw', maxWidth: '1400px' }"
          :draggable="false"
          @hide="destroyMap">

    <div class="map-section">

      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div class="field">
          <label for="latInput">Szerokość geograficzna</label>
          <InputNumber id="latInput"
                       v-model="location.lat"
                       mode="decimal"
                       :minFractionDigits="0"
                       :maxFractionDigits="20"
                       locale="en-US"
                       :useGrouping="false"
                       class="w-full"
                       :class="{ 'p-invalid': errors.location || isLatOutOfRange }" />
          <Message size="small" severity="error" v-if="isLatOutOfRange">Szerokość geograficzna musi mieścić się w przedziale od -90 do 90</Message>
          <Message size="small" severity="error" v-else-if="errors.location">{{ errors.location }}</Message>
        </div>

        <div class="field">
          <label for="lngInput">Długość geograficzna</label>
          <InputNumber id="lngInput"
                       v-model="location.lng"
                       mode="decimal"
                       :minFractionDigits="0"
                       :maxFractionDigits="20"
                       locale="en-US"
                       :useGrouping="false"
                       class="w-full"
                       :class="{ 'p-invalid': errors.location || isLngOutOfRange }" />
          <Message size="small" severity="error" v-if="isLngOutOfRange">Długość geograficzna musi mieścić się w przedziale od -180 do 180</Message>
        </div>
      </div>

      <div class="map-toolbar">
        <ToggleButton v-model="showStations"
                      onLabel="Stacje ładowania"
                      offLabel="Stacje ładowania"
                      onIcon="pi pi-eye"
                      offIcon="pi pi-eye-slash"
                      class="stations-toggle"
                      @change="toggleStations" />
        <span v-if="loadingStations" class="stations-loading">
          <i class="pi pi-spin pi-spinner" /> Wczytywanie...
        </span>
        <span v-if="stationsInRadius !== null" class="radius-count">
          <i class="pi pi-bolt" />
          <strong>{{ stationsInRadius }}</strong>
          {{ odmienStacje(stationsInRadius) }} w zasięgu
        </span>
      </div>

      <div class="map-wrapper" :class="{ 'p-invalid-map': errors.location || isLatOutOfRange || isLngOutOfRange }">
        <div ref="mapContainer" class="picker-map"></div>
      </div>
    </div>

    <template #footer>
      <Button label="Anuluj" severity="secondary" @click="close" />
      <Button label="Zapisz"
              icon="pi pi-check"
              :disabled="isSaveDisabled"
              @click="submitForm" />
    </template>
  </Dialog>
</template>

<script setup>
  import { ref, computed, watch, nextTick, onBeforeUnmount } from 'vue';
  import Dialog from 'primevue/dialog';
  import Button from 'primevue/button';
  import InputNumber from 'primevue/inputnumber';
  import ToggleButton from 'primevue/togglebutton';
  import Message from 'primevue/message';
  import L from 'leaflet';
  import 'leaflet/dist/leaflet.css';
  import 'leaflet.markercluster/dist/MarkerCluster.css';
  import 'leaflet.markercluster/dist/MarkerCluster.Default.css';
  import 'leaflet.markercluster';
  import apiClient from '../../services/api';

  const RADIUS = 850;

  const props = defineProps({
    location: {
      type: Object,
      default: () => ({ lat: null, lng: null }),
    },
  });

  const emit = defineEmits(['saved', 'close']);

  const location = ref({ lat: null, lng: null });
  const visible = ref(false);
  const mapContainer = ref(null);
  const errors = ref({});

  let leafletMap = null;
  let marker = null;
  let circle = null;
  let stationMarkersLayer = null;
  let cachedPoints = null;
  let mapInitialized = false;

  const showStations = ref(true);
  const loadingStations = ref(false);
  const stationsInRadius = ref(null);

  const isLatOutOfRange = computed(() => {
    const lat = location.value.lat;
    return typeof lat === 'number' && !isNaN(lat) && (lat < -90 || lat > 90);
  });

  const isLngOutOfRange = computed(() => {
    const lng = location.value.lng;
    return typeof lng === 'number' && !isNaN(lng) && (lng < -180 || lng > 180);
  });

  const isSaveDisabled = computed(() => {
    const lat = location.value.lat;
    const lng = location.value.lng;

    const isLatEmpty = lat === null || lat === undefined || lat === '';
    const isLngEmpty = lng === null || lng === undefined || lng === '';

    return isLatEmpty || isLngEmpty || isLatOutOfRange.value || isLngOutOfRange.value;
  });

  const odmienStacje = (n) =>
    n === 1 ? 'stacja' : n >= 2 && n <= 4 ? 'stacje' : 'stacji';

  const isValidCoordinate = (lat, lng) => {
    return typeof lat === 'number' && typeof lng === 'number' &&
      !isNaN(lat) && !isNaN(lng) &&
      lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
  };

  const fetchPoints = async () => {
    if (cachedPoints) return cachedPoints;
    loadingStations.value = true;
    try {
      const res = await apiClient.get('/pools');
      cachedPoints = res.data;
      return cachedPoints;
    } catch {
      return [];
    } finally {
      loadingStations.value = false;
    }
  };

  const stationIcon = L.divIcon({
    className: '',
    html: `<div class="station-dot"></div>`,
    iconSize: [16, 16],
    iconAnchor: [8, 8],
  });

  const renderStations = (points) => {
    if (!leafletMap || !points) return;
    if (stationMarkersLayer) {
      stationMarkersLayer.clearLayers();
    } else {
      stationMarkersLayer = L.markerClusterGroup({
        maxClusterRadius: 40,
        spiderfyOnMaxZoom: true,
        showCoverageOnHover: false,
        zoomToBoundsOnClick: true,
      });
      leafletMap.addLayer(stationMarkersLayer);
    }
    if (!showStations.value) return;

    const markers = points.map(point => {
      const m = L.marker([point.lat, point.lng], { icon: stationIcon });
      const name = point.name || point.Name || '—';
      const operator = point.operator || point.Operator || '—';
      m.bindTooltip(
        `<strong>${name}</strong><br/><span style="color:#6b7280">${operator}</span>`,
        { direction: 'top', offset: [0, -10] }
      );
      return m;
    });
    stationMarkersLayer.addLayers(markers);
  };

  const toggleStations = async () => {
    if (!showStations.value) {
      if (stationMarkersLayer) stationMarkersLayer.clearLayers();
    } else {
      const points = await fetchPoints();
      renderStations(points);
    }
  };

  const countStationsInRadius = () => {
    if (!location.value || !isValidCoordinate(location.value.lat, location.value.lng) || !cachedPoints) {
      stationsInRadius.value = null;
      return;
    }
    const center = L.latLng(location.value.lat, location.value.lng);
    stationsInRadius.value = cachedPoints.filter(p =>
      center.distanceTo(L.latLng(p.lat, p.lng)) <= RADIUS
    ).length;
  };

  const initMap = async (centerLat = 52.0692, centerLng = 19.4803, zoom = 6) => {
    if (!mapContainer.value || mapInitialized) return;
    mapInitialized = true;
    leafletMap = L.map(mapContainer.value, { center: [centerLat, centerLng], zoom });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
    }).addTo(leafletMap);

    leafletMap.on('click', (e) => {
      location.value = { lat: e.latlng.lat, lng: e.latlng.lng };
      placeMarker(e.latlng, false);
    });

    const points = await fetchPoints();
    renderStations(points);
  };

  const placeMarker = (latlng, panTo = true) => {
    errors.value.location = null;

    if (marker) marker.remove();
    if (circle) circle.remove();

    const icon = L.divIcon({
      className: '',
      html: `<div class="custom-marker"><div class="marker-pin"></div><div class="marker-pulse"></div></div>`,
      iconSize: [30, 30],
      iconAnchor: [15, 15],
    });

    marker = L.marker(latlng, { icon }).addTo(leafletMap);
    circle = L.circle(latlng, {
      radius: RADIUS,
      color: '#2563eb', fillColor: '#2563eb',
      fillOpacity: 0.08, weight: 2, dashArray: '6 4',
    }).addTo(leafletMap);

    if (panTo) {
      leafletMap.fitBounds(circle.getBounds(), { padding: [20, 20], maxZoom: 15 });
    }

    countStationsInRadius();
  };

  const clearMarker = () => {
    if (marker) { marker.remove(); marker = null; }
    if (circle) { circle.remove(); circle = null; }
    stationsInRadius.value = null;
  };

  watch(
    () => [location.value.lat, location.value.lng],
    ([newLat, newLng]) => {
      if (!leafletMap) return;

      if (isValidCoordinate(newLat, newLng)) {
        const currentLatLng = marker ? marker.getLatLng() : null;
        if (!currentLatLng || currentLatLng.lat !== newLat || currentLatLng.lng !== newLng) {
          placeMarker(L.latLng(newLat, newLng), true);
        }
      } else {
        clearMarker();
      }
    }
  );

  const destroyMap = () => {
    if (leafletMap) {
      leafletMap.remove();
      leafletMap = null;
      marker = null;
      circle = null;
      stationMarkersLayer = null;
      mapInitialized = false;
    }
  };

  const open = () => {
    visible.value = true;

    if (props.location && isValidCoordinate(props.location.lat, props.location.lng)) {
      location.value = { lat: props.location.lat, lng: props.location.lng };
    } else {
      location.value = { lat: null, lng: null };
    }

    nextTick(async () => {
      const hasValidCoords = isValidCoordinate(location.value.lat, location.value.lng);
      const startLat = hasValidCoords ? location.value.lat : 52.0692;
      const startLng = hasValidCoords ? location.value.lng : 19.4803;
      const startZoom = hasValidCoords ? 15 : 6;

      await initMap(startLat, startLng, startZoom);

      if (hasValidCoords) {
        placeMarker(L.latLng(location.value.lat, location.value.lng), true);
      }
    });
  };

  const close = () => {
    visible.value = false;
    destroyMap();
    emit('close');
  };

  const validate = () => {
    const e = {};
    if (isSaveDisabled.value) {
      e.location = 'Wpisz poprawne współrzędne lub wybierz punkt na mapie';
      errors.value = e;
      return false;
    }
    errors.value = {};
    return true;
  };

  const submitForm = () => {
    if (!validate()) return;
    emit('saved', { lat: location.value.lat, lng: location.value.lng });
    close();
  };

  onBeforeUnmount(destroyMap);

  defineExpose({ open });
</script>

<style scoped>
  .field {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
  }

  .map-section {
    display: flex;
    flex-direction: column;
    height: max(calc(100vh - 330px), 500px);
    min-height: 500px;
    overflow: visible;
  }

  .map-toolbar {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid #e5e7eb;
    flex-shrink: 0;
    flex-wrap: wrap;
  }

  .stations-toggle {
    font-size: 0.8125rem;
  }

  :deep(.stations-toggle.p-highlight) {
    background: #1d4ed8 !important;
    border-color: #1d4ed8 !important;
    color: #fff !important;
  }

  .stations-loading {
    font-size: 0.75rem;
    color: #6b7280;
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .radius-count {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: 5px;
    font-size: 0.8125rem;
    color: #1d4ed8;
    background: #eff6ff;
    border: 1px solid #bfdbfe;
    border-radius: 99px;
    padding: 3px 10px;
  }

    .radius-count .pi {
      font-size: 0.75rem;
    }

  .map-wrapper {
    flex: 1;
    position: relative;
  }

    .map-wrapper.p-invalid-map {
      outline: 2px solid #ef4444;
      outline-offset: -2px;
    }

  .picker-map {
    width: 100%;
    height: 100%;
    min-height: 100%;
    cursor: crosshair;
  }
</style>

<style>
  .station-dot {
    width: 16px;
    height: 16px;
    background: #16a34a;
    border: 2.5px solid #fff;
    border-radius: 50%;
    box-shadow: 0 1px 4px rgba(0,0,0,0.35);
  }

  .custom-marker {
    position: relative;
    width: 30px;
    height: 30px;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .marker-pin {
    width: 16px;
    height: 16px;
    background: #2563eb;
    border: 3px solid #fff;
    border-radius: 50%;
    box-shadow: 0 2px 6px rgba(37,99,235,0.5);
    z-index: 2;
    position: relative;
  }

  .marker-pulse {
    position: absolute;
    width: 30px;
    height: 30px;
    border-radius: 50%;
    background: rgba(37,99,235,0.6);
    animation: pulse-marker 1.8s ease-out infinite;
  }

  @keyframes pulse-marker {
    0% {
      transform: scale(0.4);
      opacity: 1;
    }

    100% {
      transform: scale(1.2);
      opacity: 0;
    }
  }
</style>
