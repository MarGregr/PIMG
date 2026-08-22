<template>
  <div class="station-details">
    <div v-if="loading" class="loading-state">
      <ProgressSpinner style="width: 50px; height: 50px"
                       strokeWidth="4"
                       animationDuration=".7s"
                       aria-label="Ładowanie danych..." />
      <p class="loading-text">Ładowanie danych stacji...</p>
    </div>

    <div v-else-if="error" class="error-state">
      {{ error }}
    </div>

    <div v-else-if="pool" class="details-content">
      <h2>{{ pool.Name }}</h2>
      <hr />

      <div class="info-group">
        <label>Adres:</label>
        <p>{{ pool.Address || 'Brak adresu w bazie' }}</p>
      </div>
      <div class="info-group">
        <label>Operator:</label>
        <p>{{ pool.OperatorName || 'Brak informacji' }}</p>
      </div>

      <hr />

      <div class="info-group">
        <label>Godziny otwarcia:</label>

        <ul v-if="pool.OperationHours && pool.OperationHours.length" class="hours-list">
          <li v-for="hour in pool.OperationHours" :key="hour.DayId">
            <span class="day-name">{{ hour.Day }}:</span>
            <span class="day-hours">{{ hour.From }} - {{ hour.To }}</span>
          </li>
        </ul>

        <p v-else class="no-data">Brak informacji</p>
      </div>

      <hr />

      <div class="info-group">
        <label>Liczba ładowań:</label>
        <PoolChargingChart :stats="pool.ChargingStats" />
      </div>

      <div class="info-group">
        <label>Punkty ładowania:</label>
        <div class="points-grid">
          <div v-for="point in pool.Points" :key="point.Id" class="point-card">
            <div class="point-header">
              <span class="point-code">{{ point.Code }}</span>
              <span :class="['status-badge', point.Availability == 0 ? 'broken' : (point.Status === 1 ? 'available' : 'occupied')]">
                {{ point.Availability == 1 ? (point.Status === 1 ? 'Wolny' : 'Zajęty') : 'Niedostępny' }}
              </span>
            </div>

            <div class="point-details">
              <p><strong>Cena:</strong> {{ point.Price.toFixed(2) }} PLN/kWh</p>

              <div v-if="point.Charging && point.Charging.length">
                <p v-for="(charge, idx) in point.Charging" :key="idx" class="tech-info">
                  Moc: {{ charge.Power }} kW (Tryb: {{ charge.ModeName }})
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
  import { ref, watch, onMounted } from 'vue';
  import ProgressSpinner from 'primevue/progressspinner';
  import PoolChargingChart from './PoolChargingChart.vue';
  import apiClient from '../services/api';

  const props = defineProps({
    stationId: {
      type: [String, Number],
      required: true
    }
  });

  const pool = ref(null);
  const loading = ref(false);
  const error = ref(null);

  const fetchPoolDetails = async (id) => {
    if (!id) return;

    loading.value = true;
    error.value = null;
    pool.value = null;

    try {
      const response = await apiClient.get(`/pools/${id}`);
      pool.value = response.data;
    } catch (err) {
      console.error("Błąd API:", err);
      error.value = "Wystąpił błąd podczas ładowania danych.";
    } finally {
      loading.value = false;
    }
  };

  //Obserwacja zmiany ID
  watch(() => props.stationId, (newId) => {
    fetchPoolDetails(newId);
  });

  //Pobranie danych przy pierwszym zamontowaniu komponentu
  onMounted(() => {
    fetchPoolDetails(props.stationId);
  });
</script>

<style scoped>
  .station-details {
    font-family: sans-serif;
    color: #333;
    max-width: 500px;
  }

  .loading-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 40px 20px;
  }

  .loading-text {
    margin-top: 15px;
    color: #666;
    font-size: 0.95rem;
  }

  .mb-4 {
    margin-bottom: 1.5rem;
  }

  .mb-2 {
    margin-bottom: 0.5rem;
  }

  .error-state {
    padding: 20px;
    text-align: center;
    color: #d9534f;
  }

  .details-content h2 {
    margin-top: 0;
    font-size: 1.4rem;
    color: #2c3e50;
  }

  hr {
    border: 0;
    border-top: 1px solid #eee;
    margin: 15px 0;
  }

  .info-group {
    margin-bottom: 15px;
  }

    .info-group label {
      display: block;
      font-size: 0.85rem;
      color: #7f8c8d;
      text-transform: uppercase;
      margin-bottom: 5px;
      font-weight: bold;
    }

    .info-group p {
      margin: 0;
      font-size: 1rem;
    }

  .hours-list {
    list-style: none;
    padding: 0;
    margin: 0;
  }

    .hours-list li {
      display: flex;
      justify-content: space-between;
      padding: 3px 0;
      font-size: 0.95rem;
      border-bottom: 1px dashed #f5f5f5;
    }

  .day-name {
    font-weight: 500;
  }

  .day-hours {
    color: #555;
  }

  .points-grid {
    display: flex;
    flex-direction: column;
    gap: 10px;
    margin-top: 5px;
  }

  .point-card {
    border: 1px solid #e0e0e0;
    border-radius: 6px;
    padding: 12px;
    background-color: #fafafa;
  }

  .point-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
  }

  .point-code {
    font-family: monospace;
    font-weight: bold;
    font-size: 0.95rem;
    color: #2c3e50;
  }

  .point-details p {
    font-size: 0.9rem;
    color: #555;
    margin-bottom: 4px;
  }

  .tech-info {
    font-weight: bold;
    color: #34495e;
  }

  .status-badge {
    display: inline-block;
    padding: 3px 8px;
    border-radius: 4px;
    font-size: 0.8rem;
    font-weight: bold;
    background-color: #e0e0e0;
  }

    .status-badge.available {
      background-color: #2ecc71;
      color: white;
    }

    .status-badge.occupied {
      background-color: #e74c3c;
      color: white;
    }

    .status-badge.broken {
      background-color: #dddddd;
      color: black;
    }

  .no-data {
    color: #7f8c8d;
    font-style: italic;
    font-size: 0.95rem;
    margin: 0;
  }
</style>
