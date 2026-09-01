<template>
  <Dialog v-model:visible="visible"
          modal
          header="Punkt ładowania"
          :style="{ width: '50vw', maxWidth: '1000px' }"
          :draggable="false">

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <div class="field">
        <label for="powerInput">Moc</label>
        <InputNumber id="powerInput"
                     v-model="chargingPointData.power"
                     mode="decimal"
                     suffix=" kW"
                     :min="1"
                     :minFractionDigits="0"
                     :maxFractionDigits="0"
                     locale="pl-PL"
                     :useGrouping="false"
                     class="w-full"
                     :class="{ 'p-invalid': isPowerInvalid || errors.power }" />
        <Message size="small" severity="error" v-if="isPowerInvalid">Moc musi być liczbą całkowitą większą od 0</Message>
        <Message size="small" severity="error" v-else-if="errors.power">{{ errors.power }}</Message>
      </div>

      <div class="field">
        <label for="priceInput">Cena / kWh</label>
        <InputNumber id="priceInput"
                     v-model="chargingPointData.price"
                     mode="currency"
                     currency="PLN"
                     locale="pl-PL"
                     :min="0"
                     :minFractionDigits="2"
                     :maxFractionDigits="2"
                     class="w-full"
                     :class="{ 'p-invalid': isPriceInvalid || errors.price }" />
        <Message size="small" severity="error" v-if="isPriceInvalid">Cena musi być kwotą większą lub równą 0 zł</Message>
        <Message size="small" severity="error" v-else-if="errors.price">{{ errors.price }}</Message>
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
  import { ref, computed } from 'vue';
  import Dialog from 'primevue/dialog';
  import Button from 'primevue/button';
  import InputNumber from 'primevue/inputnumber';
  import Message from 'primevue/message';

  const emit = defineEmits(['saved', 'close']);

  const visible = ref(false);
  const errors = ref({});
  const chargingPointData = ref({ power: null, price: null });

  const isPowerInvalid = computed(() => {
    const val = chargingPointData.value.power;
    if (val === null || val === undefined || val === '') return false;
    return typeof val !== 'number' || isNaN(val) || val <= 0 || !Number.isInteger(val);
  });

  const isPriceInvalid = computed(() => {
    const val = chargingPointData.value.price;
    if (val === null || val === undefined || val === '') return false;
    return typeof val !== 'number' || isNaN(val) || val < 0;
  });

  const isSaveDisabled = computed(() => {
    const power = chargingPointData.value.power;
    const price = chargingPointData.value.price;

    const isPowerEmpty = power === null || power === undefined || power === '';
    const isPriceEmpty = price === null || price === undefined || price === '';

    return isPowerEmpty || isPriceEmpty || isPowerInvalid.value || isPriceInvalid.value;
  });

  const open = (data = null) => {
    if (data) {
      chargingPointData.value = { ...data };
    } else {
      chargingPointData.value = {
        power: null,
        price: null
      };
    }
    errors.value = {};
    visible.value = true;
  };

  const close = () => {
    visible.value = false;
    emit('close');
  };

  const validate = () => {
    const e = {};
    if (isSaveDisabled.value) {
      if (chargingPointData.value.power === null) e.power = 'Moc jest wymagana';
      if (chargingPointData.value.price === null) e.price = 'Cena jest wymagana';
      errors.value = e;
      return false;
    }
    errors.value = {};
    return true;
  };

  const submitForm = () => {
    if (!validate()) return;
    emit('saved', {
      power: chargingPointData.value.power,
      price: chargingPointData.value.price
    });
    close();
  };

  defineExpose({ open });
</script>

<style scoped>
  .field {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
  }
</style>
