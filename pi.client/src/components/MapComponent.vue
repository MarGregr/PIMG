<template>
  <div class="dashboard-layout">
    <div class="map-container">

      <div v-if="!isDataLoaded" class="map-loader-overlay">
        <div class="spinner"></div>
        <p>Wczytywanie...</p>
      </div>

      <div class="icon-toolbar">
        <button class="toolbar-btn" title="Przybliż" @click="zoomIn">
          <i class="pi pi-plus"></i>
        </button>
        <button class="toolbar-btn" title="Oddal" @click="zoomOut">
          <i class="pi pi-minus"></i>
        </button>

        <div class="toolbar-divider"></div>

        <button class="toolbar-btn"
                :class="{ active: activePanel === 'filter' }"
                title="Filtry i wyszukiwanie"
                @click="togglePanel('filter')">
          <i class="pi pi-filter"></i>
        </button>

        <button class="toolbar-btn"
                :class="{ active: showOnlyFavorites }"
                title="Tylko ulubione"
                @click="toggleFavoritesOnly">
          <i :class="showOnlyFavorites ? 'pi pi-heart-fill' : 'pi pi-heart'"></i>
        </button>

        <button class="toolbar-btn"
                :class="{ active: showCountyLayer }"
                title="Warstwa powiatów"
                @click="showCountyLayer = !showCountyLayer">
          <i :class="showCountyLayer ? 'pi pi-eye' : 'pi pi-eye-slash'"></i>
        </button>
      </div>

      <div v-if="activePanel === 'filter'" class="filter-panel">
        <div class="filter-panel-header">
          <h4>Filtry</h4>
          <button class="panel-close-btn" @click="activePanel = null">✕</button>
        </div>

        <div class="filter-panel-body">
          <div class="field-block">
            <label class="field-label">Szukaj adresu lub miasta</label>
            <span class="p-input-icon-left search-address-wrapper">
              <i class="pi pi-search search-icon" />
              <AutoComplete v-model="selectedAddress"
                            :suggestions="addressSuggestions"
                            :showClear="true"
                            field="display_name"
                            placeholder="np. Warszawa, ul. Marszałkowska"
                            class="address-search"
                            append-to="self"
                            @complete="searchAddress"
                            @option-select="onAddressSelect"
                            optionLabel="display_name"
                            @keydown.enter="onEnterSearch" />
            </span>
          </div>

          <div class="field-block">
            <label class="field-label">Operator</label>
            <MultiSelect v-model="selectedOperators"
                         :options="operatorOptions"
                         placeholder="Wszyscy operatorzy"
                         :maxSelectedLabels="2"
                         :filter="true"
                         filterPlaceholder="Szukaj operatora..."
                         class="station-filter"
                         @change="updateMapMarkers" />
          </div>
        </div>
      </div>

      <l-map ref="mapRef" v-model:zoom="zoom" :center="center" :options="{ zoomControl: false }">
        <l-tile-layer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                      layer-type="base"
                      name="OpenStreetMap"></l-tile-layer>

        <l-geo-json v-if="geoJsonData && isDataLoaded && showCountyLayer"
                    :geojson="geoJsonData"
                    :options="geoJsonOptions"
                    :options-style="geoJsonStyleFn"
                    @ready="() => console.log('geojson ready, options:', geoJsonOptions)" />

        <l-marker-cluster-group ref="clusterGroupRef">
        </l-marker-cluster-group>
      </l-map>
    </div>

    <div v-if="selectedPool" class="side-panel">
      <div class="panel-header">
        <div class="header-title-area">
          <h3>Szczegóły stacji</h3>
          <button @click="toggleFavorite(selectedPool.id)" class="favorite-btn" :class="{ 'is-favorite': isFavorite(selectedPool.id) }">
            <i :class="isFavorite(selectedPool.id) ? 'pi pi-heart-fill' : 'pi pi-heart'"></i>
          </button>
        </div>
        <button @click="closePanel" class="close-btn">✕</button>
      </div>
      <div class="panel-content">
        <PoolDetails :stationId="selectedPool.id" />
      </div>
    </div>

  </div>
</template>

<script setup>
  import { ref, onMounted, nextTick, computed } from 'vue';
  import PoolDetails from './PoolDetails.vue';
  import MultiSelect from 'primevue/multiselect';
  import Divider from 'primevue/divider';
  import AutoComplete from 'primevue/autocomplete';

  import 'leaflet/dist/leaflet.css';
  import 'leaflet.markercluster/dist/MarkerCluster.css';
  import 'leaflet.markercluster/dist/MarkerCluster.Default.css';

  import apiClient from '../services/api';

  import L from 'leaflet'
  import { LMap, LTileLayer, LGeoJson } from '@vue-leaflet/vue-leaflet';
  import { LMarkerClusterGroup } from 'vue-leaflet-markercluster';

  window.L = L

  const zoom = ref(7);
  const center = ref([52.0692, 19.4803]);

  const allPoints = ref([]);
  const selectedOperators = ref([]);

  const clusterGroupRef = ref(null);
  const selectedPool = ref(null);

  const mapRef = ref(null);
  const isDataLoaded = ref(false);
  const geoJsonData = ref(null);
  const vehicleStatsMap = ref({});

  const showCountyLayer = ref(true);
  const showOnlyFavorites = ref(false);

  // Pasek ikon: który panel jest aktywnie otwarty ('filter' albo null)
  const activePanel = ref(null);

  const togglePanel = (name) => {
    activePanel.value = activePanel.value === name ? null : name;
  };

  const toggleFavoritesOnly = () => {
    showOnlyFavorites.value = !showOnlyFavorites.value;
    updateMapMarkers();
  };

  // Wyszukiwarka adresów
  const selectedAddress = ref(null);
  const addressSuggestions = ref([]);

  const favoriteStationIds = ref([]);

  const searchAddress = async (event) => {
    const query = event.query;
    if (query.length < 3) return;

    try {
      const response = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&countrycodes=pl&limit=5`
      );
      const data = await response.json();
      addressSuggestions.value = data;
    } catch (err) {
      console.error('Błąd podczas wyszukiwania adresu:', err);
    }
  };

  const jumpToLocation = (lat, lng, zoomLevel = 14) => {
    if (mapRef.value && mapRef.value.leafletObject) {
      mapRef.value.leafletObject.setView([lat, lng], zoomLevel);
    }
  };

  const onAddressSelect = (event) => {
    const location = event.value;
    jumpToLocation(parseFloat(location.lat), parseFloat(location.lon));
  };

  const onEnterSearch = () => {
    if (addressSuggestions.value.length > 0) {
      const first = addressSuggestions.value[0];
      jumpToLocation(parseFloat(first.lat), parseFloat(first.lon));
      selectedAddress.value = first;
    }
  };

  const fetchFavorites = async () => {
    try {
      const response = await apiClient.get('/favorites/pools');
      favoriteStationIds.value = response.data.map(f => f.poolId);
    } catch (err) {
      console.error('Błąd pobierania ulubionych:', err);
    }
  };

  const toggleFavorite = async (id) => {
    try {
      if (isFavorite(id)) {
        await apiClient.delete(`/favorites/pools/${id}`);
        favoriteStationIds.value = favoriteStationIds.value.filter(favId => favId !== id);
      } else {
        await apiClient.post(`/favorites/pools/${id}`);
        favoriteStationIds.value.push(id);
      }

      if (showOnlyFavorites.value) {
        updateMapMarkers();
      }
    } catch (err) {
      console.error('Błąd aktualizacji ulubionych:', err);
    }
  };

  const isFavorite = (id) => favoriteStationIds.value.includes(id);

  const operatorOptions = computed(() => {
    const operators = allPoints.value.map(p => p.operator).filter(Boolean);
    return [...new Set(operators)].sort();
  });

  //Kolorowanie warstwy powiatów
  const getColor = (d) => {
    return d > 1000 ? '#800026' :
      d > 750 ? '#BD0026' :
        d > 500 ? '#E31A1C' :
          d > 300 ? '#FC4E2A' :
            d > 150 ? '#FD8D3C' :
              d > 100 ? '#FEB24C' :
                d > 50 ? '#FED976' :
                  '#FFEDA0';
  };

  const geoJsonOptions = computed(() => {
    return {
      onEachFeature: (feature, layer) => {
        const countyKey = feature.properties.name || feature.properties.nazwa || '';
        const cleanKey = countyKey.toLowerCase().trim();

        const count = vehicleStatsMap.value[cleanKey] || 0;
        layer.bindPopup(`<strong>Powiat:</strong> ${countyKey}<br/><strong>Zarejestrowane BEV:</strong> ${count}`);
      }
    };
  });

  const geoJsonStyleFn = computed(() => {
    return (feature) => {
      const countyKey = feature.properties.name || feature.properties.nazwa || '';
      const cleanKey = countyKey.toLowerCase().trim();
      const count = Number(vehicleStatsMap.value[cleanKey] || 0);

      return {
        fillColor: getColor(count),
        weight: 0.5,
        opacity: 1,
        color: '#666',
        fillOpacity: 0.5
      };
    };
  });

  const updateMapMarkers = () => {
    const clusterGroup = clusterGroupRef.value?.leafletObject;
    if (!clusterGroup) return;

    clusterGroup.clearLayers();

    const filteredPoints = allPoints.value.filter(point => {
      if (showOnlyFavorites.value && !isFavorite(point.id)) {
        return false;
      }
      if (selectedOperators.value.length === 0) return true;
      return selectedOperators.value.includes(point.operator);
    });

    const markersArray = filteredPoints.map(point => {
      const marker = L.marker([point.lat, point.lng]);
      marker.on('click', () => {
        selectedPool.value = point;
      });
      return marker;
    });

    clusterGroup.addLayers(markersArray);
  };

  const fetchPoints = async () => {
    try {
      isDataLoaded.value = false;

      const response = await apiClient.get('/pools');
      allPoints.value = response.data;

      const responseStats = await apiClient.get('/reports/powiaty');
      const rawStatsArray = responseStats.data;

      const statsMap = {};
      rawStatsArray.forEach(item => {
        if (item.powiat) {
          const key = item.powiat.toLowerCase().trim();
          statsMap[key] = item.vehicles || item.pojazdy || 0;
        }
      });
      vehicleStatsMap.value = statsMap;

      const responseGeoJson = await fetch('/assets/poland.counties.json');
      geoJsonData.value = await responseGeoJson.json();

      isDataLoaded.value = true;

      await nextTick();
      updateMapMarkers();

    } catch (err) {
      console.error("Błąd ładowania danych mapy:", err);
      isDataLoaded.value = true;
    }
  };

  const closePanel = async () => {
    selectedPool.value = null;
    await nextTick();
    if (mapRef.value && mapRef.value.leafletObject) {
      mapRef.value.leafletObject.invalidateSize();
    }
  };

  const zoomIn = () => {
    if (mapRef.value && mapRef.value.leafletObject) {
      mapRef.value.leafletObject.zoomIn();
    }
  };

  const zoomOut = () => {
    if (mapRef.value && mapRef.value.leafletObject) {
      mapRef.value.leafletObject.zoomOut();
    }
  };

  onMounted(() => {
    fetchFavorites();
    fetchPoints();
  });
</script>

<style scoped>
  .dashboard-layout {
    display: flex;
    width: 100%;
    height: 93vh;
    overflow: hidden;
    position: relative;
  }

  .map-container {
    flex-grow: 1;
    height: 100%;
    position: relative;
    isolation: isolate;
  }

  .map-loader-overlay {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(255, 255, 255, 0.85);
    z-index: 2000;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  }

    .map-loader-overlay p {
      color: #333;
      font-weight: 600;
      font-size: 1.1rem;
      margin-top: 15px;
    }

  .spinner {
    border: 4px solid rgba(0, 0, 0, 0.1);
    width: 50px;
    height: 50px;
    border-radius: 50%;
    border-left-color: #05a6f0;
    animation: spin 1s linear infinite;
  }

  @keyframes spin {
    0% {
      transform: rotate(0deg);
    }

    100% {
      transform: rotate(360deg);
    }
  }

  /* Pionowy pasek ikon po lewej stronie mapy */
  .icon-toolbar {
    position: absolute;
    top: 15px;
    left: 15px;
    z-index: 1100;
    background: white;
    border-radius: 8px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  .toolbar-btn {
    width: 40px;
    height: 40px;
    background: white;
    border: none;
    border-bottom: 1px solid #eee;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    color: #555;
    font-size: 1.05rem;
    transition: background 0.15s, color 0.15s;
  }

    .toolbar-btn:last-child {
      border-bottom: none;
    }

    .toolbar-btn:hover {
      background: #f3f3f3;
    }

    .toolbar-btn.active {
      background: #05a6f0;
      color: #ffffff;
    }

  .toolbar-divider {
    height: 1px;
    background-color: #eee;
    width: 100%;
  }


  /* Panel filtrów otwierany ikoną lejka */
  .filter-panel {
    position: absolute;
    top: 95px;
    left: 65px;
    z-index: 1100;
    background: white;
    border-radius: 8px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    width: 400px;
    display: flex;
    flex-direction: column;
  }

  .filter-panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 12px;
    border-bottom: 1px solid #eee;
  }

    .filter-panel-header h4 {
      margin: 0;
      font-size: 0.95rem;
      color: #333;
    }

  .panel-close-btn {
    background: none;
    border: none;
    font-size: 1rem;
    cursor: pointer;
    color: #888;
    line-height: 1;
  }

    .panel-close-btn:hover {
      color: #000;
    }

  .filter-panel-body {
    padding: 12px;
    display: flex;
    flex-direction: column;
    gap: 14px;
  }

  .field-block {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .field-label {
    font-size: 0.75rem;
    font-weight: 600;
    color: #777;
    text-transform: uppercase;
    letter-spacing: 0.03em;
  }

  .search-address-wrapper {
    position: relative;
    width: 100%;
  }

  .search-icon {
    position: absolute;
    left: 10px;
    top: 50%;
    transform: translateY(-50%);
    z-index: 10;
    color: #6c757d;
    pointer-events: none;
  }

  :deep(.address-search),
  :deep(.address-search input) {
    width: 100%;
  }

  :deep(.address-search input) {
    padding-left: 2.5rem;
    font-size: 0.85rem;
  }

  :deep(.p-autocomplete-panel) {
    z-index: 9999 !important;
  }

  .station-filter {
    width: 100%;
  }

  .side-panel {
    width: 450px;
    background-color: #ffffff;
    box-shadow: -2px 0 10px rgba(0, 0, 0, 0.1);
    display: flex;
    flex-direction: column;
    z-index: 1000;
    animation: slideIn 0.1s ease-out;
  }

  .panel-header {
    padding: 15px;
    background-color: #f5f5f5;
    border-bottom: 1px solid #ddd;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .header-title-area {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .panel-header h3 {
    margin: 0;
  }

  .favorite-btn {
    background: none;
    border: none;
    font-size: 1.3rem;
    cursor: pointer;
    color: #b5b5b5;
    transition: transform 0.2s, color 0.2s;
    padding: 0;
    display: flex;
    align-items: center;
  }

    .favorite-btn:hover {
      transform: scale(1.15);
      color: #e74c3c;
    }

    .favorite-btn.is-favorite {
      color: #e74c3c;
    }

  .close-btn {
    background: none;
    border: none;
    font-size: 1.2rem;
    cursor: pointer;
    color: #666;
  }

    .close-btn:hover {
      color: #000;
    }

  .panel-content {
    padding: 20px;
    overflow-y: auto;
  }

  @keyframes slideIn {
    from {
      transform: translateX(100%);
    }

    to {
      transform: translateX(0);
    }
  }
</style>
