<template>

  <ProjectMapModal ref="modalRef"
                   :location="location"
                   @saved="onSaved"
                   @close="modalVisible = false" />

  <div class="mt-10 md:ms-10 md:me-10">

    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
      <!--Kolumna 1-->
      <div>
        <!--Nazwa-->
        <div class="field">
          <label for="projectName">Nazwa projektu</label>
          <InputText id="projectName"
                     v-model="form.name"
                     class="w-full"
                     :class="{ 'p-invalid': errors.name }"
                     @input="clearError('name')" />
          <Message size="small" severity="error" v-if="errors.name">{{ errors.name }}</Message>
        </div>

        <div class="mt-2 field location-field">
          <!--Lokalizacja-->
          <label>Lokalizacja</label>
          <div class="map-status has-location" :class="{ 'p-invalid-border': errors.location }">
            <template v-if="hasLocation">
              <i class="pi pi-map-marker" />
              {{ form.location.lat }}, {{ form.location.lng }}
              <Button label="Edytuj" size="small" severity="info" @click="showModal" style="margin-left: auto;" />
            </template>
            <template v-else>
              <Button label="Wybierz lokalizację" severity="info" size="small" @click="showModal" />
            </template>
          </div>
          <Message size="small" severity="error" v-if="errors.location">{{ errors.location }}</Message>
        </div>
      </div>

      <!--Kolumna 2-->
      <div>
        <div class="field">
          <label for="projectDesc">Opis</label>
          <Textarea id="projectDesc"
                    v-model="form.description"
                    rows="5"
                    class="w-full"
                    :class="{ 'p-invalid': errors.description }"
                    @input="clearError('description')"
                    autoResize />
          <Message size="small" severity="error" v-if="errors.description">{{ errors.description }}</Message>
        </div>
      </div>

    </div>

    <!--Lista punktów ładowania-->
    <div class="mt-2">
      <ChargingPointsList :chargingPoints="form.chargingPoints" @changed="onChargingPointsChanged" />
      <Message size="small" severity="error" v-if="errors.chargingPoints" class="mt-1">{{ errors.chargingPoints }}</Message>
    </div>

    <div class="mt-6 flex justify-end gap-2">
      <Button label="Anuluj" severity="secondary" @click="onCancel" />
      <Button label="Zapisz"
              icon="pi pi-check"
              @click="submitForm" />
    </div>
  </div>
</template>

<script setup>
  import { ref, computed, nextTick } from 'vue';
  import { useRouter } from 'vue-router';
  import InputText from 'primevue/inputtext';
  import Textarea from 'primevue/textarea';
  import Message from 'primevue/message';
  import Button from 'primevue/button';
  import apiClient from '../../services/api';
  import ProjectMapModal from './ProjectMapModal.vue';
  import ChargingPointsList from './ChargingPointsList.vue';

  const router = useRouter();

  const form = ref({
    name: '',
    description: '',
    location: { lat: null, lng: null },
    chargingPoints: [],
  });

  const location = ref(null);
  const modalRef = ref(null);
  const modalVisible = ref(false);
  const errors = ref({});

  //computed określaja czy lokalizacja jest wybrana
  const hasLocation = computed(() => {
    return form.value.location?.lat !== null &&
      form.value.location?.lng !== null &&
      form.value.location?.lat !== undefined &&
      form.value.location?.lng !== undefined;
  });

  const isSaveDisabled = computed(() => {
    return !form.value.name.trim() ||
      !form.value.description.trim() ||
      !hasLocation.value ||
      form.value.chargingPoints.length === 0;
  });

  const clearError = (field) => {
    if (errors.value[field]) {
      delete errors.value[field];
    }
  };

  const onSaved = (payload) => {
    form.value.location.lat = payload.lat;
    form.value.location.lng = payload.lng;
    clearError('location');
  };

  const onChargingPointsChanged = (updatedPoints) => {
    form.value.chargingPoints = updatedPoints;
    clearError('chargingPoints');
  };

  const showModal = async () => {
    location.value = form.value.location;
    modalVisible.value = true;
    await nextTick();
    modalRef.value?.open();
  };

  const validate = () => {
    const errs = {};

    if (!form.value.name || !form.value.name.trim()) {
      errs.name = 'Nazwa projektu jest wymagana';
    }

    if (!form.value.description || !form.value.description.trim()) {
      errs.description = 'Opis projektu jest wymagany';
    }

    if (!hasLocation.value) {
      errs.location = 'Lokalizacja na mapie jest wymagana';
    }

    if (!form.value.chargingPoints || form.value.chargingPoints.length === 0) {
      errs.chargingPoints = 'Musisz dodać co najmniej jeden punkt ładowania';
    }

    errors.value = errs;
    return Object.keys(errs).length === 0;
  };

  const submitForm = async () => {
    if (!validate()) return;

    try {
      //const method = isEditing ? 'PUT' : 'POST';
      //const url = isEditing ? `/projects/${projectId}` : '/projects';
      //const response = await apiClient({ method, url, data: payload });


      await apiClient.post('/projects', form.value);
      router.push('/projects');
    } catch (err) {
      console.error('Błąd podczas zapisywania projektu:', err);
    }
  };

  const onCancel = () => {
    router.push('/projects');
  };
</script>

<style scoped>
  .field {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
  }

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

    .map-status.p-invalid-border {
      border-color: #eab308;
      border-style: solid;
    }

  .location-field {
    padding-top: 0;
  }
</style>
