<script>
    import { onMount } from 'svelte';
    import './main.css';  
   
    let disableRun = true;
    let key = '';
    let allKeys = [];
    let filter = '';
    let allFilters = [];
     

    $:{
        disableRun = ( !key ); 
    }    

    async function run (e) { 
        try {
                // const response = await fetch('/report/ais/reports',{   //серверная машина
                const response = await fetch('/service/reports',{ // Локальная машина
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: 
                    JSON.stringify({                        
                        //dateEnd: dateEnd ? dateEnd : null,
                        //mmsi: mmsi ? mmsi.split(/\r?\n/g).map(s => parseInt(s, 10)) : [],
                        key,
                        filter
                    })
                    
                });
                await response.json();
                alert('Запрос отправлен на сервер');
            }
        catch (e) {           	
            alert('Ошибка: отправка не завершена');
        }
    }  
  
    onMount(() => { 
       // fetch ('/report/ais/sources')  // серверная машина
       fetch ('/service/sources') // локальная машина
        .then(response => response.json())
        .then(data => {
           // allKeys = data.key2.concat(data.key1);
            allKeys = data.key;
            key = allKeys[0].name;
        })
        .catch(e => console.log(e));
    });

    onMount(() => { 
       // fetch ('/report/ais/sources')  // серверная машина
       fetch ('/service/sources') // локальная машина
        .then(response => response.json())
        .then(data => {
           // allKeys = data.key2.concat(data.key1);
            allFilters = data.filter;
            filter = allFilters[0].name;
        })
        .catch(e => console.log(e));
    });
  

</script>
<div class="header">
    <div class="serviceScanex">
        <span class="tagline">Service</span>
    </div>
</div>
<div class="wrapper">
    <div class="allKeys">
        <label>Ключ :</label>
        <select bind:value="{key}">
            {#each allKeys as ak}
                <option value="{ak.name}">{ak.label}</option>
            {/each}
        </select>
    </div>
    <div class="allFilters">
        <label>Фильтр :</label>
        <select bind:value="{filter}">
            {#each allFilters as af}
                <option value="{af.name}">{af.label}</option>
            {/each}
        </select>
    </div>
</div>
 <div class="footer"> 
    <button class="button" disabled="{disableRun}" on:click|stopPropagation="{run}">Получить</button>
</div>  