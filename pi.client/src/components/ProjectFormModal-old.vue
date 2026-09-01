<template>
  <Dialog v-model:visible="visible"
          modal
          :header="editingProject ? 'Edytuj projekt' : 'Nowy projekt stacji ładowania'"
          :style="{ width: '95vw', maxWidth: '1400px' }"
          :draggable="false"
          class="project-dialog"
          @hide="destroyMap">
    <div class="dialog-body">

      <!-- Zakładki na widoku mobilnym -->
      <div class="mobile-tabs">
        <button class="tab-btn" :class="{ active: activeTab === 'form' }" @click="activeTab = 'form'">
          <i class="pi pi-list" /> Dane
        </button>
        <button class="tab-btn" :class="{ active: activeTab === 'map' }" @click="activeTab = 'map'; onMapTabActivated()">
          <i class="pi pi-map" /> Mapa
          <span v-if="errors.location" class="tab-error-dot" />
        </button>
      </div>

      <!-- Formularz -->
      <div class="form-section" :class="{ 'tab-hidden': activeTab !== 'form' }">
        <div class="field">
          <label for="projectName">Nazwa projektu</label>
          <InputText id="projectName"
                     v-model="form.name"
                     placeholder=""
                     class="w-full"
                     :class="{ 'p-invalid': errors.name }" />
          <Message size="small" severity="error" v-if="errors.name">{{ errors.name }}</Message>
        </div>

        <div class="field">
          <label for="projectDesc">Opis</label>
          <Textarea id="projectDesc"
                    v-model="form.description"
                    placeholder=""
                    rows="3"
                    class="w-full"
                    autoResize />
        </div>

        <div class="field location-field">
          <label>Lokalizacja</label>
          <div class="map-status" :class="{ 'has-location': form.location }">
            <template v-if="form.location">
              <i class="pi pi-map-marker" />
              {{ form.location.lat.toFixed(5) }}, {{ form.location.lng.toFixed(5) }}
              <button class="clear-location" @click="clearLocation" title="Usuń lokalizację">
                <i class="pi pi-times" />
              </button>
            </template>
            <template v-else>
              <i class="pi pi-crosshairs" />
              Kliknij na mapie, aby ustawić punkt
            </template>
          </div>
          <Message size="small" severity="error" v-if="errors.location">{{ errors.location }}</Message>
        </div>

        <!-- Na widoku mobilnym przejście do widoku mapy, widoczne gdy brak lokalizacji -->
        <button v-if="!form.location" class="go-to-map-btn" @click="activeTab = 'map'; onMapTabActivated()">
          <i class="pi pi-map" /> Przejdź do mapy i wybierz lokalizację
        </button>

        <div class="field">
          <label>Zasięg analizy</label>
          <div class="radius-control">
            <Slider v-model="form.radius"
                    :min="100"
                    :max="10000"
                    :step="100"
                    class="radius-slider" />
            <div class="radius-value">
              <span class="radius-number">{{ formattedRadius }}</span>
              <span class="radius-unit">{{ radiusUnit }}</span>
            </div>
          </div>
          <div class="radius-hint">
            <i class="pi pi-info-circle" />
            W tym zasięgu będą analizowane istniejące stacje ładowania
          </div>
        </div>

        <Button v-if="form.location" label='Wylicz'
                icon="pi pi-calculator"
                :loading="loadingPrediction"
                @click="predict" />
        <span v-if="loadingPrediction" class="stations-loading">
          <i class="pi pi-spin pi-spinner" /> Obliczanie...
        </span>
        <div v-if="!loadingPrediction">Predykcja: {{ predictionValue }}</div>

      </div>

      <!-- Mapa -->
      <div class="map-section" :class="{ 'tab-hidden': activeTab !== 'map' }">
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
            {{ pluralStacje(stationsInRadius) }} w zasięgu
          </span>
        </div>
        <div class="map-wrapper" :class="{ 'p-invalid-map': errors.location }">
          <div ref="mapContainer" class="picker-map"></div>
        </div>
      </div>

    </div>

    <template #footer>
      <div class="dialog-footer">
        <Button label="Anuluj" severity="secondary" text @click="close" />
        <Button :label="editingProject ? 'Zapisz zmiany' : 'Utwórz projekt'"
                icon="pi pi-check"
                :loading="saving"
                @click="submitForm" />
      </div>
    </template>
  </Dialog>
</template>

<script setup>
  import { ref, computed, watch, nextTick, onBeforeUnmount } from 'vue';
  import Dialog from 'primevue/dialog';
  import Button from 'primevue/button';
  import InputText from 'primevue/inputtext';
  import Textarea from 'primevue/textarea';
  import Slider from 'primevue/slider';
  import ToggleButton from 'primevue/togglebutton';
  import Message from 'primevue/message';
  import L from 'leaflet';
  import 'leaflet/dist/leaflet.css';
  import 'leaflet.markercluster/dist/MarkerCluster.css';
  import 'leaflet.markercluster/dist/MarkerCluster.Default.css';
  import 'leaflet.markercluster';
  import apiClient from '../services/api';

  /**
   * @typedef {Object} Project
   * @property {string} id
   * @property {string} name
   * @property {string} description
   * @property {number} lat
   * @property {number} lng
   * @property {number} radius
   * @property {string} createdAt
   * @property {string} updatedAt
   */

  const props = defineProps({
    editingProject: {
      type: Object,
      default: null,
    },
  });

  const emit = defineEmits(['saved', 'close']);

  const visible = ref(true);
  const saving = ref(false);
  const mapContainer = ref(null);
  const activeTab = ref('form');

  let leafletMap = null;
  let marker = null;
  let circle = null;
  let stationMarkersLayer = null;
  let cachedPoints = null;
  let mapInitialized = false;

  const showStations = ref(true);
  const loadingStations = ref(false);
  const stationsInRadius = ref(null);

  const loadingPrediction = ref(false);
  const predictionValue = ref(0);

  const form = ref({
    name: '',
    description: '',
    location: null,
    radius: 1000,
  });
  const errors = ref({});

  const initForm = () => {
    if (props.editingProject) {
      form.value = {
        name: props.editingProject.name,
        description: props.editingProject.description,
        radius: props.editingProject.radius,
        location: { lat: props.editingProject.lat, lng: props.editingProject.lng },
      };
    } else {
      form.value = { name: '', description: '', location: null, radius: 1000 };
    }
    errors.value = {};
  };

  const formattedRadius = computed(() =>
    form.value.radius >= 1000
      ? (form.value.radius / 1000).toFixed(1).replace('.0', '')
      : form.value.radius
  );

  const radiusUnit = computed(() =>
    form.value.radius >= 1000 ? 'km' : 'm'
  );

  const pluralStacje = (n) =>
    n === 1 ? 'stacja' : n >= 2 && n <= 4 ? 'stacje' : 'stacji';

  const fetchPoints = async () => {
    if (cachedPoints) return cachedPoints;
    loadingStations.value = true;
    try {
      const res = await apiClient.get('/pools');
      cachedPoints = res.data;
      return cachedPoints;
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
    if (!leafletMap) return;
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
    if (!form.value.location || !cachedPoints) { stationsInRadius.value = null; return; }
    const center = L.latLng(form.value.location.lat, form.value.location.lng);
    stationsInRadius.value = cachedPoints.filter(p =>
      center.distanceTo(L.latLng(p.lat, p.lng)) <= form.value.radius
    ).length;
  };

  const initMap = async (centerLat = 52.0692, centerLng = 19.4803, zoom = 6) => {
    await nextTick();
    if (!mapContainer.value || mapInitialized) return;
    mapInitialized = true;
    leafletMap = L.map(mapContainer.value, { center: [centerLat, centerLng], zoom });
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
    }).addTo(leafletMap);
    leafletMap.on('click', (e) => placeMarker(e.latlng));
    const points = await fetchPoints();
    renderStations(points);
  };

  //Na widoku mobilnym mapa jest ukryta przy inicjalizacji
  const onMapTabActivated = async () => {
    await nextTick();
    if (!mapInitialized) {
      if (props.editingProject) {
        await initMap(props.editingProject.lat, props.editingProject.lng, 11);
        placeMarker(L.latLng(props.editingProject.lat, props.editingProject.lng));
      } else {
        await initMap();
      }
    } else if (leafletMap) {
      leafletMap.invalidateSize();
    }
  };

  const placeMarker = (latlng) => {
    form.value.location = { lat: latlng.lat, lng: latlng.lng };
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
      radius: form.value.radius,
      color: '#2563eb', fillColor: '#2563eb',
      fillOpacity: 0.08, weight: 2, dashArray: '6 4',
    }).addTo(leafletMap);
    leafletMap.fitBounds(circle.getBounds(), { padding: [40, 40], maxZoom: 13 });
    countStationsInRadius();
  };

  const updateCircle = () => {
    if (!circle || !form.value.location) return;
    circle.setRadius(form.value.radius);
    leafletMap.fitBounds(circle.getBounds(), { padding: [40, 40], maxZoom: 13 });
    countStationsInRadius();
  };

  const clearLocation = () => {
    form.value.location = null;
    stationsInRadius.value = null;
    if (marker) { marker.remove(); marker = null; }
    if (circle) { circle.remove(); circle = null; }
  };

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
    initForm();
    activeTab.value = 'form';
    visible.value = true;
    //Na desktop mapa inicjalizowana od razu
    nextTick(async () => {
      const isMobile = window.matchMedia('(max-width: 640px)').matches;
      if (!isMobile) {
        if (props.editingProject) {
          await initMap(props.editingProject.lat, props.editingProject.lng, 11);
          placeMarker(L.latLng(props.editingProject.lat, props.editingProject.lng));
        } else {
          await initMap();
        }
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
    if (!form.value.name.trim()) e.name = 'Nazwa projektu jest wymagana';
    if (!form.value.location) e.location = 'Wybierz lokalizację na mapie';
    errors.value = e;
    return Object.keys(e).length === 0;
  };

  const submitForm = async () => {
    if (!validate()) return;
    saving.value = true;
    try {
      const isEditing = !!props.editingProject;
      const projectId = isEditing ? props.editingProject.id : crypto.randomUUID();

      /** @type {Project} */
      const payload = {
        id: projectId,
        name: form.value.name.trim(),
        description: form.value.description.trim(),
        lat: form.value.location.lat,
        lng: form.value.location.lng,
        radius: form.value.radius,
      };

      const method = isEditing ? 'PUT' : 'POST';
      const url = isEditing ? `/projects/${projectId}` : '/projects';

      const response = await apiClient({ method, url, data: payload });

      emit('saved', response.data);
      close();
    } catch (error) {
      console.error('Błąd podczas zapisu projektu:', error);
      if (error.response && error.response.status === 404) {
        errors.value.name = 'Brak uprawnień do edycji tego projektu lub projekt nie istnieje.';
      } else {
        errors.value.name = 'Wystąpił błąd serwera podczas zapisu.';
      }
    } finally {
      saving.value = false;
    }
  };

  const predict = async () => {
    let projectId = props.editingProject.id;

    loadingPrediction.value = true;
    predictionValue.value = 0;
    try {
      const url = `/projects/${projectId}/predict`;
      const res = await apiClient.get(url);
      const result = res.data;
      predictionValue.value = result;
    } catch (error) {
      console.error('Błąd podczas zapisu projektu:', error);
      if (error.response && error.response.status === 404) {
        errors.value.name = 'Brak uprawnień do edycji tego projektu lub projekt nie istnieje.';
      }
    } finally {
      loadingPrediction.value = false;
    }

  };


  watch(() => form.value.radius, updateCircle);

  onBeforeUnmount(destroyMap);

  defineExpose({ open });
</script>



<style scoped>
  :deep(.project-dialog .p-dialog-content) {
    padding: 0;
    display: flex;
    flex-direction: column;
    flex: 1;
    overflow: hidden;
  }

  :deep(.project-dialog .p-dialog-header) {
    border-bottom: 1px solid #e5e7eb;
    padding: 1rem 1.25rem;
  }

  /* Mobilne */
  .mobile-tabs {
    display: none;
    flex-shrink: 0;
  }

  .dialog-body {
    display: grid;
    grid-template-columns: 320px 1fr;
    height: min(calc(100vh - 260px), 720px);
    overflow: hidden;
  }

  /* Formularz */
  .form-section {
    padding: 1.25rem 1.25rem 1rem;
    border-right: 1px solid #e5e7eb;
    display: flex;
    flex-direction: column;
    gap: 1.1rem;
    overflow-y: auto;
    height: 100%;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
  }

    .field label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: #374151;
      letter-spacing: 0.02em;
      text-transform: uppercase;
    }

  /* Mapa */
  .map-section {
    display: flex;
    flex-direction: column;
    height: 100%;
    overflow: hidden;
  }

  .map-toolbar {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid #e5e7eb;
    background: #f9fafb;
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
    min-height: 0;
  }

    .map-wrapper.p-invalid-map {
      outline: 2px solid #ef4444;
      outline-offset: -2px;
    }

  .picker-map {
    width: 100%;
    height: 100%;
    cursor: crosshair;
  }

  /* Wybór promienia */
  .radius-control {
    display: flex;
    align-items: center;
    gap: 1rem;
  }

  .radius-slider {
    flex: 1;
  }

  .radius-value {
    display: flex;
    align-items: baseline;
    gap: 3px;
    min-width: 52px;
  }

  .radius-number {
    font-size: 1.1rem;
    font-weight: 600;
    color: #1d4ed8;
    line-height: 1;
  }

  .radius-unit {
    font-size: 0.75rem;
    color: #6b7280;
  }

  .radius-hint {
    display: flex;
    align-items: flex-start;
    gap: 0.4rem;
    font-size: 0.75rem;
    color: #6b7280;
    line-height: 1.4;
  }

    .radius-hint .pi {
      font-size: 0.75rem;
      flex-shrink: 0;
      margin-top: 1px;
    }

  /* Lokalizacja */
  .map-status {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.8125rem;
    color: #9ca3af;
    background: #f9fafb;
    border: 1px dashed #d1d5db;
    border-radius: 6px;
    padding: 0.5rem 0.75rem;
    min-height: 36px;
  }

    .map-status.has-location {
      color: #1d4ed8;
      background: #eff6ff;
      border-color: #bfdbfe;
      border-style: solid;
    }

  .location-field {
    padding-top: 0;
  }

  .clear-location {
    margin-left: auto;
    background: none;
    border: none;
    cursor: pointer;
    color: #6b7280;
    padding: 2px 4px;
    border-radius: 4px;
    display: flex;
    align-items: center;
    font-size: 0.75rem;
  }

    .clear-location:hover {
      color: #dc2626;
      background: #fee2e2;
    }

  /* Skrót "Go to map" (tylko na mobilnym) */
  .go-to-map-btn {
    display: none;
    width: 100%;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    padding: 0.625rem 1rem;
    background: #eff6ff;
    border: 1px solid #bfdbfe;
    border-radius: 8px;
    color: #1d4ed8;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: background 0.15s;
  }

    .go-to-map-btn:hover {
      background: #dbeafe;
    }

  .dialog-footer {
    display: flex;
    justify-content: flex-end;
    gap: 0.5rem;
  }

  @media (max-width: 900px) and (min-width: 641px) {
    .dialog-body {
      grid-template-columns: 260px 1fr;
      height: min(calc(100vh - 260px), 680px);
    }
  }

  @media (max-width: 640px) {
    :deep(.project-dialog .p-dialog-header) {
      padding: 0.875rem 1rem;
      font-size: 0.9375rem;
    }

    /* Zakładki widoczne */
    .mobile-tabs {
      display: flex;
      border-bottom: 1px solid #e5e7eb;
      background: #f9fafb;
      flex-shrink: 0;
    }

    .tab-btn {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      padding: 0.625rem 0.5rem;
      background: none;
      border: none;
      border-bottom: 2px solid transparent;
      color: #6b7280;
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
      position: relative;
      transition: color 0.15s, border-color 0.15s;
    }

      .tab-btn.active {
        color: #1d4ed8;
        border-bottom-color: #1d4ed8;
      }

    .tab-error-dot {
      width: 6px;
      height: 6px;
      background: #ef4444;
      border-radius: 50%;
      position: absolute;
      top: 8px;
      right: calc(50% - 28px);
    }

    .dialog-body {
      display: flex;
      flex-direction: column;
      height: calc(100dvh - 160px);
      overflow: hidden;
      grid-template-columns: unset;
    }

    /* Ukrywanie nieaktywnej zakładki */
    .tab-hidden {
      display: none !important;
    }

    /* Scrollowalny formularz  */
    .form-section {
      border-right: none;
      border-bottom: none;
      flex: 1;
      overflow-y: auto;
      -webkit-overflow-scrolling: touch;
      padding: 1rem;
      gap: 1rem;
    }

    /* Skrót do mapy */
    .go-to-map-btn {
      display: flex;
    }

    .location-field {
      margin-top: 0;
      padding-top: 0;
    }

    .map-section {
      flex: 1;
      height: 100%;
      min-height: 0;
    }

    .map-toolbar {
      padding: 0.4rem 0.6rem;
      gap: 0.4rem;
    }

    .radius-count {
      font-size: 0.75rem;
      padding: 2px 8px;
    }
  }
</style>

<style>
  /* Globalne */
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
    background: rgba(37,99,235,0.2);
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
