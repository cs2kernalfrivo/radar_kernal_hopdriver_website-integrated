from django.urls import path
from . import views

urlpatterns = [
    # The page with the map (e.g., http://127.0.0.1:8000/)
    path('', views.radar_view, name='radar_view'),
    
    # The API endpoint for C# (e.g., http://127.0.0.1:8000/api/radar/update/)
    path('api/radar/update/', views.update_radar_api, name='update_radar_api'),
]