from django.urls import path
from . import views

urlpatterns = [
    # Existing path for the radar map
    path('', views.radar_view, name='radar_view'),

    # NEW: The page that will show the full ESP
    path('esp/', views.esp_view, name='esp_view'),
    
    # The API endpoint for C# (this will now feed both pages)
    path('api/radar/update/', views.update_radar_api, name='update_radar_api'),
]