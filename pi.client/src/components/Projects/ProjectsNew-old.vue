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
                     placeholder=""
                     class="w-full"
                     :class="{ 'p-invalid': errors.name }" />
          <Message size="small" severity="error" v-if="errors.name">{{ errors.name }}</Message>
        </div>

        <div class="mt-2 field location-field">
          <!--Lokalizacja-->
          <label>Lokalizacja</label>
          <div class="map-status" :class="{ 'has-location': form.location }">
            <template v-if="form.location.lat && form.location.lng">
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
                    placeholder=""
                    rows="5"
                    class="w-full"
                    autoResize />
        </div>
      </div>


    </div>

    <div class="mt-2">
      <ChargingPointsList :chargingPoint="form.chargingPoints" />
    </div>

    <div class="mt-6 flex justify-end gap-2">
      <Button label="Anuluj" severity="secondary" @click="onCancel" />
      <Button label="Zapisz"
              icon="pi pi-check"
              :disabled="isSaveDisabled"
              @click="submitForm" />
    </div>
  </div>
</template>

<script setup>
  import { ref, computed, watch, nextTick, onBeforeUnmount } from 'vue';
  import { useRouter } from 'vue-router'
  import Dialog from 'primevue/dialog';
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
  import apiClient from '../../services/api';
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import Button from 'primevue/button';
  import Tag from 'primevue/tag';
  import ProjectMapModal from './ProjectMapModal.vue'
  import ChargingPointsList from './ChargingPointsList.vue'

  defineProps({
  });

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

  const onSaved = (payload) => {
    form.value.location.lat = payload.lat;
    form.value.location.lng = payload.lng;

    console.log("form:", form);
  };

  //Otwieranie modalu
  const showModal = async () => {
    location.value = form.value.location;
    modalVisible.value = true;
    await nextTick();
    modalRef.value?.open();
  };


  const onCancel = () => {
    router.push("/projects");
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

  .location-field {
    padding-top: 0;
  }
</style>
