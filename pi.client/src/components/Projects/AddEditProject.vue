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
          <div class="map-status" :class="{ 'has-location': hasLocation, 'p-invalid-border': errors.location }">
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

        <div class="mt-2 field-block">
          <!--Operator-->
          <label class="field-label">Operator</label>
          <Select v-model="selectedOperator"
                  :options="operators"
                  optionLabel="name"
                  filterBy="name"
                  class="w-full"
                  :filter="true"
                  filterPlaceholder="Szukaj operatora..."
                  checkmark
                  @change="clearError('operator')" />
          <Message size="small" severity="error" v-if="errors.operator">{{ errors.operator }}</Message>
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

    <!--Predykcja-->
    <div class="mt-6 flex gap-2">
      <div style="border:solid 1px #8888ff; border-radius:4px; padding:6px; min-width: 75px">
        {{predictValue}}
      </div>
      <Button label="Predykcja"
              icon="pi pi-calculator"
              :loading="isSubmitting"
              @click="predict" />
    </div>

    <!--Zapis-->
    <div class="mt-6 flex justify-end gap-2">
      <Button label="Anuluj" severity="secondary" @click="onCancel" />
      <Button label="Zapisz"
              icon="pi pi-check"
              :loading="isSubmitting"
              @click="submitForm" />
    </div>
  </div>

  <!--Blokada ekranu-->
  <BlockUI :blocked="isBlocked" :fullScreen="true">
    <template #default>
      <div v-if="isBlocked" class="loading-overlay">
        <ProgressSpinner style="width: 50px; height: 50px" strokeWidth="4" />
        <p>Trwa przetwarzanie danych, proszę czekać...</p>
      </div>
    </template>
  </BlockUI>
</template>

<script setup>
  import { ref, computed, nextTick, onMounted } from 'vue';
  import { useRouter, useRoute } from 'vue-router';
  import InputText from 'primevue/inputtext';
  import Textarea from 'primevue/textarea';
  import Message from 'primevue/message';
  import Button from 'primevue/button';
  import apiClient from '../../services/api';
  import Dialog from 'primevue/dialog';
  import Select from 'primevue/select';
  import ProjectMapModal from './ProjectMapModal.vue';
  import ChargingPointsList from './ChargingPointsList.vue';
  import BlockUI from 'primevue/blockui';
  import ProgressSpinner from 'primevue/progressspinner';


  const isBlocked = ref(false);

  const router = useRouter();
  const route = useRoute();

  const projectId = computed(() => route.params.id);
  const isEditing = computed(() => !!projectId.value);

  const operators = ref([]);
  const selectedOperator = ref(null);

  const predictValue = ref(null);;

  const isSubmitting = ref(false);

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

  const fetchOperators = async () => {
    try {
      const response = await apiClient.get('/operators');
      operators.value = response.data;
      // const temp = operators.value;
      // console.log(temp);
    } catch (err) {
      console.error('Błąd podczas pobierania operatorów:', err);
    }
  };

  const fetchDefaultUserOperator = async () => {
    //Wywołanie tylko dla nowego projektu
    if (isEditing.value) return;

    try {
      const response = await apiClient.get('/settings');
      const userSettings = response.data;

      const defaultOperatorId = userSettings?.operator_id;

      //Przypisanie jeśli użytkownik ma ustawionego domyślnego operatora w bazie
      if (defaultOperatorId && operators.value.length > 0) {
        selectedOperator.value = operators.value.find(op => op.id === defaultOperatorId) || null;
      }
    } catch (err) {
      console.error('Błąd podczas pobierania ustawień użytkownika:', err);
    }
  };

  //computed określa czy lokalizacja jest wybrana
  const hasLocation = computed(() => {
    return form.value.location?.lat !== null &&
      form.value.location?.lng !== null &&
      form.value.location?.lat !== undefined &&
      form.value.location?.lng !== undefined;
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

  const fetchProjectData = async () => {
    if (!isEditing.value) return;

    try {
      const response = await apiClient.get(`/projects/${projectId.value}`);
      const data = response.data

      form.value = {
        name: data.name ?? '',
        description: data.description ?? '',
        location: {
          lat: data.lat ?? null,
          lng: data.lng ?? null,
        },
        chargingPoints: data.chargingPoints ?? [],
      };

      // const cos1 = data.operatorId
      // const cos2 = data.operator_id
      // const projectOperatorId = data.operatorId ?? data.operator_id;
      // if (projectOperatorId && operators.value.length > 0) {
      if (data.operatorId) {
        selectedOperator.value = operators.value.find(op => op.id === data.operatorId) || null;
      }

    } catch (err) {
      console.error('Błąd podczas pobierania danych projektu:', err);
    }
  };

  onMounted(async () => {
    await fetchOperators();
    await fetchDefaultUserOperator();
    await fetchProjectData();
  });

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

    if (!selectedOperator.value) {
      errs.operator = 'Operator jest wymagany';
    }

    if (!form.value.chargingPoints || form.value.chargingPoints.length === 0) {
      errs.chargingPoints = 'Musisz dodać co najmniej jeden punkt ładowania';
    }

    errors.value = errs;
    return Object.keys(errs).length === 0;
  };

  const submitForm = async () => {
    if (!validate()) return;

    isSubmitting.value = true;
    try {

      const payload = {
        id: projectId.value,
        name: form.value.name.trim(),
        description: form.value.description.trim(),
        operatorId: selectedOperator.value?.id,
        lat: form.value.location.lat,
        lng: form.value.location.lng,
        chargingPoints: form.value.chargingPoints,
      };

      const method = isEditing.value ? 'put' : 'post';
      const url = isEditing.value ? `/projects/${projectId.value}` : '/projects';

      const response = await apiClient({ method, url, data: payload });
      router.push('/projects');
    } catch (err) {
      console.error('Błąd podczas zapisywania projektu:', err);
    } finally {
      isSubmitting.value = false;
    }
  };

  const predict = async () => {
    if (!validate()) return;

    isBlocked.value = true;

    isSubmitting.value = true;
    try {



      const payload = {
        id: projectId.value,
        name: form.value.name.trim(),
        description: form.value.description.trim(),
        operatorId: selectedOperator.value?.id,
        lat: form.value.location.lat,
        lng: form.value.location.lng,
        chargingPoints: form.value.chargingPoints,
      };

      const method = 'post';
      const url = '/projects/predict';

      const response = await apiClient({ method, url, data: payload });

      predictValue.value = `${(response.data * 100).toFixed(2)} %`
      // router.push('/projects');
    } catch (err) {
      console.error('Błąd podczas predyckji:', err);
    } finally {
      isSubmitting.value = false;
      isBlocked.value = false;
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
